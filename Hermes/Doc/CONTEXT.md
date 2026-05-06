# SYSTEM PROMPT & PROJECT CONTEXT FOR AI ASSISTANT
## 1. PROJECT OVERVIEW
- Project Name: Hermes Chat App
- Domain: Cybersecurity & Networking (Secure E2EE Chat & P2P Voice)
- Tech Stack: C# .NET 8, WPF (MVVM Pattern), MySQL, SignalR, Firebase Authentication.
- Required NuGet Packages:
	- Database: MySqlConnector and Dapper.
	- WebSockets (Signaling): Microsoft.AspNetCore.SignalR.Client.
	- Voice & Media: NAudio (for capturing/playing audio).
	- NAT Traversal: STUN.Client (or custom UDP STUN implementation).
	- Auth: FirebaseAuthentication.net (version 3.72 by stephenbannan) or Firebase Admin SDK.
	- Environment: DotNetEnv.
- Networking: Database communication is strictly routed through a Tailscale SD-WAN (WireGuard) VPN tunnel.
- Core Principle: Zero-Knowledge Architecture. The server (MySQL/SignalR) must NEVER hold unencrypted private keys or plaintext messages. For Voice Calls, media must flow Peer-to-Peer (UDP) without touching the server.
## 2. DATABASE SCHEMA (MySQL)
The database consists of 7 normalized tables. Use this schema for all queries.
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
    TimeToLive INT DEFAULT 0, -- Burn-on-read TTL in seconds (0 = permanent, -1 = view once)
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

### A. Cryptography & E2EE Flow
- Key Wrapping: Use PBKDF2 (Rfc2898DeriveBytes, HMAC-SHA256, 300,000 iterations) with User's Password + Salt to derive a 256-bit AES Master Key. Use this AES Master Key to encrypt the RSA Private Key.
- Sending Message: Generate random AES-256 Session Key. Encrypt message with Session Key. Encrypt Session Key with recipients' RSA Public Keys. Format: Base64(Nonce + Tag + CipherText).
- Receiving Message: Unwrap RSA Private Key. Decrypt Session Key. Decrypt CipherText.
### B. Burn-on-read & View Once Mechanism
- TimeToLive = -1 (View Once): Hide decrypted message behind "Tap to View". When user closes popup, IMMEDIATELY remove from WPF UI and call API to DELETE.
- TimeToLive > 0 (Timer): Show countdown timer. Start C# DispatcherTimer when IsRead = TRUE. Call DELETE when expired.
- Double-layer Deletion: Fallback to local SQLite/JSON queue if network fails. Server sweeps DB every 5 mins.
### C. P2P Voice Call (UDP Hole Punching & STUN)
- When asked to implement Voice Call logic, use System.Net.Sockets.UdpClient and NAudio following this strict flow:
- STUN Resolution: Use a STUN Client to query stun.l.google.com:19302 to retrieve the local machine's Public IP and Port.
- TCP Signaling: Send the retrieved Public IP/Port to the callee via the SignalR Hub. Receive the callee's Public IP/Port via SignalR.
- UDP Hole Punching: Both clients concurrently send dummy UDP packets to each other's Public IP/Port to open the NAT firewall.
- Voice Streaming: - Initialize NAudio WaveInEvent (16kHz, 16-bit, Mono) to capture microphone.
- In the DataAvailable event, send the raw byte array directly via UdpClient.SendAsync().
- Run a background loop using UdpClient.ReceiveAsync(). Push received bytes into an NAudio BufferedWaveProvider linked to a WaveOutEvent to play audio.
## 4. CODING GUIDELINES
- Strictly adhere to the MVVM (Model-View-ViewModel) architectural pattern.
- Use Asynchronous programming (async/await) extensively, especially for Network Sockets and I/O.
- Properly dispose of UdpClient and NAudio resources using IDisposable to prevent memory leaks and port exhaustion.
## 5. USER COMMAND INSTRUCTIONS
- When the user asks you to implement a specific view, ViewModel, or Service, reference this context document to ensure the code complies with E2EE, DB constraints, and Low-Level Networking principles.