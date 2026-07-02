// Standardized to production level
// Purpose: Document project architecture, design patterns, security, and realtime features
// Dependencies: WPF, SignalR, MySQL, E2EE

# Hermes Chat App Architecture

## Overview
Hermes is a production-grade Desktop Chat Application built with C# (.NET 10 WPF MVVM) and ASP.NET Core SignalR. The system features end-to-end encryption (E2EE), duress passwords, and complete real-time synchronization between clients.

## Key Architectural Highlights

### 1. Real-Time Synchronization & Room Management
- **Automatic Group Enrollment:** When users initiate conversations or send messages, the SignalR server (`ChatHub`) dynamically joins recipient connections into conversation rooms via `Groups.AddToGroupAsync`.
- **Instant Notification Broadcasts:** Creation of new 1-on-1 chats via search results immediately triggers `SendNewChatNotificationAsync`, notifying remote clients to load the new conversation in real-time without manual navigation or app restarting.
- **Direct Recipient Delivery:** When sending E2EE messages, `SendMessage` delivers ciphertext directly to all participant connections across online endpoints (`_userConnections`), eliminating race conditions during room joins.

### 2. Read Receipts & UI Formatting
- **Visual Checkmarks:** Sent messages display `✓` (sent, unread by recipient). When recipients read messages, real-time read receipts (`MessagesMarkedAsRead`) update sent items to `✓✓` (read).
- **Unread Chat Formatting:** Conversations with unread incoming messages highlight the sender's display name and message snippet in **Bold** font weight (`#111827`). Selecting the chat marks items as read both locally and remotely (`MarkMessagesAsReadAsync` + `NotifyMessagesReadAsync`), returning font formatting to normal (`#6B7280`).

### 3. End-to-End Encryption (E2EE)
- **Hybrid Encryption:** Symmetric encryption via AES-GCM for message bodies combined with asymmetric RSA for session key wrapping.
- **Key Storage:** Private keys are protected using PBKDF2 key derivation and stored safely on MySQL.
