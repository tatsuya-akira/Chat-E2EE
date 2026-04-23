# SYSTEM PROMPT & PROJECT CONTEXT FOR AI ASSISTANT
## 1. PROJECT OVERVIEW
- Project Name: Hermes Chat App
- Domain: Cybersecurity (Secure E2EE Chat Application)
- Tech Stack: C# .NET 8, WPF (MVVM Pattern), MySQL, SignalR, Firebase Authentication.
- Required NuGet Packages:
-- Database: MySqlConnector and Dapper (or Pomelo.EntityFrameworkCore.MySql if using EF Core).
-- WebSockets: Microsoft.AspNetCore.SignalR.Client.
-- Auth: FirebaseAuthentication.net (by stephenbannan) or Firebase Admin SDK.
-- Environment Variables: DotNetEnv (to load connection strings and API keys).
-- Networking: Database communication is strictly routed through a Tailscale SD-WAN (WireGuard) VPN tunnel. The app connects to the DB via a 100.83.55.117 IP address. MYSQL_CONNECTION_STRING="Server=100.83.55.117;Database=hermes_db;Uid=root;Pwd=root;"
-- Core Principle: Zero-Knowledge Architecture. The server (MySQL/SignalR) must NEVER hold unencrypted private keys or plaintext messages.

## 2. DATABASE SCHEMA (MySQL)
The database consists of 7 normalized tables. Use this schema for all Entity Framework Core or ADO.NET queries.
```sql
CREATE DATABASE IF NOT EXISTS hermes_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE hermes_db;

CREATE TABLE USERS (
    Id VARCHAR(128) PRIMARY KEY, -- Firebase UID
    Email VARCHAR(255) NOT NULL UNIQUE,
    PublicKey LONGTEXT NOT NULL, -- RSA Public Key (Base64)
    WrappedPrivateKey LONGTEXT NOT NULL, -- RSA Private Key encrypted by PBKDF2 AES Master Key
    Salt VARCHAR(255) NOT NULL
) ENGINE=InnoDB;

CREATE TABLE USERINFO (
    UserId VARCHAR(128) PRIMARY KEY,
    FullName VARCHAR(100) NOT NULL UNIQUE,
    AvatarUrl TEXT,
    StatusMessage VARCHAR(255),
    CONSTRAINT fk_userinfo_user FOREIGN KEY (UserId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE CONTACTS (
    UserId VARCHAR(128) NOT NULL,
    ContactId VARCHAR(128) NOT NULL,
    IsAccepted BOOLEAN DEFAULT FALSE,
    AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (UserId, ContactId),
    CONSTRAINT fk_user_contacts FOREIGN KEY (UserId) REFERENCES USERS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_contact_users FOREIGN KEY (ContactId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE CONVERSATIONS (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    IsGroup BOOLEAN DEFAULT FALSE,
    GroupName VARCHAR(255),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

CREATE TABLE PARTICIPANTS (
    ConversationId INT NOT NULL,
    UserId VARCHAR(128) NOT NULL,
    JoinedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (ConversationId, UserId),
    CONSTRAINT fk_participants_conv FOREIGN KEY (ConversationId) REFERENCES CONVERSATIONS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_participants_user FOREIGN KEY (UserId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE MESSAGES (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ConversationId INT NOT NULL,
    SenderId VARCHAR(128) NOT NULL,
    CipherText LONGTEXT NOT NULL, -- AES Encrypted text
    TimeToLive INT DEFAULT 0, -- Burn-on-read TTL in seconds (0 = permanent)
    SentAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_messages_conv FOREIGN KEY (ConversationId) REFERENCES CONVERSATIONS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_messages_sender FOREIGN KEY (SenderId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

CREATE TABLE MESSAGE_RECIPIENTS (
    MessageId INT NOT NULL,
    RecipientId VARCHAR(128) NOT NULL,
    EncryptedSessionKey LONGTEXT NOT NULL, -- AES Session Key encrypted by Recipient's RSA Public Key
    IsRead BOOLEAN DEFAULT FALSE,
    ReadAt DATETIME NULL,
    PRIMARY KEY (MessageId, RecipientId),
    CONSTRAINT fk_messagerecipients_msg FOREIGN KEY (MessageId) REFERENCES MESSAGES(Id) ON DELETE CASCADE,
    CONSTRAINT fk_messagerecipients_recipient FOREIGN KEY (RecipientId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;
```

## 3. CORE LOGIC RULES (MUST FOLLOW STRICTLY)
### A. Cryptography & E2EE Flow (System.Security.Cryptography)
- When writing encryption/decryption services, follow this Hybrid Encryption logic strictly:
- Key Generation (On Register): Generate an RSA-2048 key pair.
- Key Wrapping: Use PBKDF2 (Rfc2898DeriveBytes, HMAC-SHA256, 300,000 iterations) with User's Password + Salt to derive a 256-bit AES Master Key. Use this AES Master Key to encrypt the RSA Private Key -> Store as WrappedPrivateKey in DB.
- Sending Message:
-- Generate a random AES-256 Session Key.
-- Encrypt the plaintext message with the Session Key -> Store as CipherText in MESSAGES.
-- For each recipient in the conversation, encrypt the Session Key using their PublicKey -> Store as EncryptedSessionKey in MESSAGE_RECIPIENTS.
-- AES-GCM Storage Format: Since the database only has one CipherText column, the CryptoService MUST concatenate the Nonce/IV (12 bytes), the Tag (16 bytes), and the actual CipherText into a single byte array before converting to Base64 for storage. Format: Base64(Nonce + Tag + CipherText). During decryption, extract the first 12 bytes as Nonce, the next 16 bytes as Tag, and the rest as CipherText.
- Receiving Message:
-- Unwrap the user's RSA Private Key using their password (via PBKDF2).
-- Use the RSA Private Key to decrypt EncryptedSessionKey to get the AES Session Key.
-- Use the AES Session Key to decrypt CipherText.
### B. Duress Password Logic (Shadow Account Trick)
- Do NOT modify the database for fake data. Use the "Shadow Account" trick at the Firebase Auth layer.
- On Login: Try SignInWithEmailAndPassword(email, password).
- Exception Handling: If FirebaseAuthException occurs (Wrong Password), DO NOT show an error immediately.
- Shadow Fallback: Automatically prepend "shadow_" to the email (e.g., shadow_user@domain.com) and try SignIn again with the same entered password.
- Result: If successful, fetch data for the shadow UID from MySQL. The app UI must act completely normal. Plausible Deniability is maintained.
### C. Burn-on-read (Disappearing Messages)
If TimeToLive > 0:
- Client-Side Trigger: When IsRead becomes true, start a local C# DispatcherTimer. When the TTL expires, remove the message from the ObservableCollection (UI) and call the API/SQL to DELETE FROM MESSAGES WHERE Id = @id. (MySQL ON DELETE CASCADE will handle the rest).
- Double-layer Deletion Mechanism:
- Client-Side (WPF): Immediately remove the message from the recipient's UI when the timer expires, regardless of network connectivity. If offline, save the deletion command (MessageId only, NO plaintext) to a local queue (e.g., SQLite or a local JSON file like pending_deletes.json). Once the network is restored, push the queued commands to the Server.
- Server-Side (Failsafe): Implement a background worker or a MySQL Event Scheduler running every 5 minutes. It must scan for records in MESSAGE_RECIPIENTS where IsRead = TRUE AND CurrentTime > (ReadAt + TimeToLive). If found, automatically delete the record without waiting for the Client API call.
- UI/UX Enhancement: When a recipient reads a message with a TTL, display a small countdown timer next to the message bubble to create a sense of urgency and improve the user experience.
## 4. CODING GUIDELINES
- Strictly adhere to the MVVM (Model-View-ViewModel) architectural pattern.
- Separate cryptographic logic into a dedicated CryptoService.
- Separate database calls into a DatabaseRepository.
- Use Asynchronous programming (async/await) to prevent UI freezing during cryptographic calculations or DB calls.
- Do NOT use metaphors or layman terms in variable names or comments (e.g., use EncryptedSessionKey not Envelope; use CipherText not SafeBox).
## 5. USER COMMAND INSTRUCTIONS
When the user asks you to implement a specific view, ViewModel, or Service, reference this context document to ensure the code complies with the E2EE architecture, Database constraints, and Shadow Account logic.