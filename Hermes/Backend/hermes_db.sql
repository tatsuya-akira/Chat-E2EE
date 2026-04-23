-- SQL Script for Hermes Chat App
-- Created for Cybersecurity Project - UIT
-- Version: 2.0 (Security Optimized)

CREATE DATABASE IF NOT EXISTS hermes_db CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE hermes_db;

-- 1. Bảng USERS: Quản lý định danh và các khóa RSA
CREATE TABLE USERS (
    Id VARCHAR(128) PRIMARY KEY, -- Firebase UID
    Email VARCHAR(255) NOT NULL UNIQUE,
    PublicKey LONGTEXT NOT NULL, -- RSA Public Key (Base64)
    WrappedPrivateKey LONGTEXT NOT NULL, -- RSA Private Key đã bị bọc bởi AES (Base64)
    Salt VARCHAR(255) NOT NULL -- Muối ngẫu nhiên cho PBKDF2
) ENGINE=InnoDB;

-- 2. Bảng USERINFO: Thông tin người dùng
CREATE TABLE USERINFO (
    UserId VARCHAR(128) PRIMARY KEY,
    FullName VARCHAR(100) NOT NULL UNIQUE,
    AvatarUrl TEXT,
    StatusMessage VARCHAR(255),
    CONSTRAINT fk_userinfo_user FOREIGN KEY (UserId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- 3. Bảng CONTACTS: Quản lý danh sách bạn bè
CREATE TABLE CONTACTS (
    UserId VARCHAR(128) NOT NULL,
    ContactId VARCHAR(128) NOT NULL,
    IsAccepted BOOLEAN DEFAULT FALSE,
    AddedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (UserId, ContactId),
    CONSTRAINT fk_contacts_user FOREIGN KEY (UserId) REFERENCES USERS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_contacts_contact FOREIGN KEY (ContactId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- 4. Bảng CONVERSATIONS: Quản lý các phòng chat
CREATE TABLE CONVERSATIONS (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    IsGroup BOOLEAN DEFAULT FALSE,
    GroupName VARCHAR(255),
    CreatedAt DATETIME DEFAULT CURRENT_TIMESTAMP
) ENGINE=InnoDB;

-- 5. Bảng PARTICIPANTS: Thành viên hội thoại
CREATE TABLE PARTICIPANTS (
    ConversationId INT NOT NULL,
    UserId VARCHAR(128) NOT NULL,
    JoinedAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    PRIMARY KEY (ConversationId, UserId),
    CONSTRAINT fk_participants_conv FOREIGN KEY (ConversationId) REFERENCES CONVERSATIONS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_participants_user FOREIGN KEY (UserId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- 6. Bảng MESSAGES: Lưu nội dung mã hóa AES
CREATE TABLE MESSAGES (
    Id INT AUTO_INCREMENT PRIMARY KEY,
    ConversationId INT NOT NULL,
    SenderId VARCHAR(128) NOT NULL,
    CipherText LONGTEXT NOT NULL, -- Nội dung đã mã hóa AES-256
    TimeToLive INT DEFAULT 0, -- Thời gian tự hủy (giây). 0 là vĩnh viễn.
    SentAt DATETIME DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT fk_messages_conv FOREIGN KEY (ConversationId) REFERENCES CONVERSATIONS(Id) ON DELETE CASCADE,
    CONSTRAINT fk_messages_sender FOREIGN KEY (SenderId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;

-- 7. Bảng MESSAGE_RECIPIENTS: Lưu khóa phiên đã mã hóa RSA
CREATE TABLE MESSAGE_RECIPIENTS (
    MessageId INT NOT NULL,
    RecipientId VARCHAR(128) NOT NULL,
    EncryptedSessionKey LONGTEXT NOT NULL, -- Khóa AES được mã hóa bởi RSA Public Key người nhận
    IsRead BOOLEAN DEFAULT FALSE,
    ReadAt DATETIME NULL,
    PRIMARY KEY (MessageId, RecipientId),
    CONSTRAINT fk_messagerecipients_msg FOREIGN KEY (MessageId) REFERENCES MESSAGES(Id) ON DELETE CASCADE,
    CONSTRAINT fk_messagerecipients_recipient FOREIGN KEY (RecipientId) REFERENCES USERS(Id) ON DELETE CASCADE
) ENGINE=InnoDB;