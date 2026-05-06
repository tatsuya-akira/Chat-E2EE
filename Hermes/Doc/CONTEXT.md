# SYSTEM PROMPT & PROJECT CONTEXT FOR AI ASSISTANT
## 1. PROJECT OVERVIEW & ARCHITECTURE
- Project Name: Hermes Chat App
- Domain: Cybersecurity & Networking (Secure E2EE Chat & P2P Voice)
- Tech Stack: C# .NET 8, WPF (MVVM), ASP.NET Core Web API, MySQL, SignalR, Firebase Auth.
- Networking: The backend is hosted on a VPS and routed through a Tailscale SD-WAN (WireGuard) VPN tunnel (IP: 100.83.55.117).
- Core Principle: Zero-Knowledge Architecture. The server must NEVER hold unencrypted private keys or plaintext messages.
### STRICT 3-TIER SOLUTION ARCHITECTURE
The Visual Studio Solution (HermesChat.sln) is strictly divided into 3 projects:
- Hermes.Server (ASP.NET Core Web API): - Role: The Backend hosted on the VPS.
-- Tasks: Handles REST API requests, SignalR Hub connections, and executes ALL MySQL database queries.
Strict Rule: Never processes media bytes (audio) or unencrypted chat data. Only handles encrypted data.
- Hermes.Client (WPF Application): - Role: The Frontend Desktop App.
-- Tasks: Handles UI/UX (MVVM), E2EE Cryptography (AES/RSA encryption/decryption), Firebase Authentication, NAudio voice capturing, and UDP Peer-to-Peer connections.
Strict Rule: MUST NEVER connect directly to the MySQL Database. All data fetching/saving must go through HTTP Requests (HttpClient) or SignalR to Hermes.Server.
- Hermes.Shared (Class Library): - Role: The data bridge.
-- Tasks: Contains shared Models, DTOs (Data Transfer Objects), Enums, and Constants. Both Client and Server reference this project.
### REQUIRED NUGET PACKAGES
- Server: MySqlConnector, Dapper (or EF Core), Microsoft.AspNetCore.SignalR.
- Client: Microsoft.AspNetCore.SignalR.Client, NAudio, STUN.Client, FirebaseAuthentication.net (v3.72 by stephenbannan), DotNetEnv.
## 2. DATABASE SCHEMA (MySQL)
IMPORTANT: This database is ONLY accessed by Hermes.Server. Use this schema for API development.
```sql=
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

## 3. CORE LOGIC RULES
### A. Cryptography & E2EE Flow (Client Project Only)
1. Key Wrapping: Use PBKDF2 (Rfc2898DeriveBytes, HMAC-SHA256, 300,000 iterations) with User's Password + Salt to derive a 256-bit AES Master Key. Use this AES Master Key to encrypt the RSA Private Key.
2. Sending Message: Generate random AES-256 Session Key. Encrypt message with Session Key. Encrypt Session Key with recipients' RSA Public Keys. Send format Base64(Nonce + Tag + CipherText) via API/SignalR.
3. Receiving Message: Unwrap RSA Private Key. Decrypt Session Key. Decrypt CipherText.
### B. Burn-on-read & View Once Mechanism
1. TimeToLive = -1 (View Once): Client hides decrypted message behind "Tap to View". When user closes popup, IMMEDIATELY remove from WPF UI and call Server API to DELETE message.
2. TimeToLive > 0 (Timer): Client shows countdown timer. Start C# DispatcherTimer when IsRead = TRUE. Call Server API to DELETE when expired.
3. Double-layer Deletion: - Client: Fallback to local SQLite/JSON queue if network fails to call API. Server: Runs a background sweeper every 5 mins to force delete expired records.
### C. P2P Voice Call (UDP Hole Punching & STUN)
When asked to implement Voice Call logic, use System.Net.Sockets.UdpClient and NAudio in the Client Project following this strict flow:
1. STUN Resolution: Use a STUN Client to query stun.l.google.com:19302 to retrieve the local machine's Public IP and Port.
2. TCP Signaling: Send the retrieved Public IP/Port to the callee via the Hermes.Server SignalR Hub.
3. UDP Hole Punching: Both clients concurrently send dummy UDP packets to each other's Public IP/Port to open the NAT firewall.
4. Voice Streaming (Client to Client): - Initialize NAudio WaveInEvent (16kHz, 16-bit, Mono) to capture microphone. In the DataAvailable event, send the raw byte array directly via UdpClient.SendAsync(). Run a background loop using UdpClient.ReceiveAsync(). Push received bytes into an NAudio BufferedWaveProvider linked to a WaveOutEvent to play audio.
## 4. CODING GUIDELINES
- Always specify which project (Server, Client, or Shared) the generated code belongs to.
- Put all request/response models and DTOs in Hermes.Shared.
-The Client must use HttpClient or SignalR to communicate with the Server.
- Strictly adhere to the MVVM pattern in Hermes.Client.
- Use Asynchronous programming (async/await) extensively.
- Properly dispose of network and audio resources (IDisposable).
## 5. USER COMMAND INSTRUCTIONS
- When the user asks you to implement a specific feature, reference this context document. Ensure the code respects the strict 3-tier architecture (Client never accesses DB), E2EE constraints, and Low-Level Networking principles.