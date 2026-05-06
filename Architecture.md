# Architecture: Hermes Desktop Messenger

## 1. Context & Tech Stack
- **Frontend**: C# WPF (.NET 10)
- **Database (Primary)**: MySQL (`hermes_db`)
- **Authentication**: Firebase Authentication
- **Real-time Synchronization**: Firebase Realtime Database
- **Architecture Pattern**: MVVM (Model-View-ViewModel) + Service-Oriented Architecture (SOA)

## 2. Database Schema (MySQL)
- `users`: Identity and RSA Keys (Id, Email, PublicKey, WrappedPrivateKey, Salt)
- `userinfo`: Profiles (UserId, FullName, AvatarUrl, StatusMessage)
- `contacts`: Friendlist (UserId, ContactId, IsAccepted, AddedAt)
- `conversations`: Chat Rooms (Id, IsGroup, GroupName, CreatedAt)
- `participants`: Room members (ConversationId, UserId, JoinedAt)
- `messages`: AES Encrypted messages (Id, ConversationId, SenderId, CipherText, TimeToLive, SentAt)
- `message_recipients`: RSA Encrypted Session Keys (MessageId, RecipientId, EncryptedSessionKey, IsRead, ReadAt)

## 3. Real-time Mechanism (Firebase RTDB)
Firebase RTDB acts as a signaling server.
- `/user_sync/{userId}`: List of conversations/signals for a specific user to force refresh their UI.
- `/conversations/{conversationId}/messages`: Real-time stream of new message IDs or content for active listeners in a chat window.
- `/conversations/{conversationId}/typing/{userId}`: Real-time typing indicators.
- `/conversations/{conversationId}/seen/{userId}`: Real-time seen indicators.

## 4. TDD & Implementation Strategy
1. **Setup**: Create `Hermes.Tests` with xUnit.
2. **Feature 1: Chat Creation**:
   - Write tests for `ChatService.CreateConversation`.
   - Implement MySQL logic (Conversations & Participants).
   - Implement Firebase RTDB signal push.
3. **Feature 2: Real-time Messaging**:
   - Write tests for `MessageService.SendMessage`.
   - Implement MySQL persistence.
   - Implement Firebase RTDB push.
   - Implement Realtime Listener in WPF.
4. **Feature 3: Status & Search**:
   - Write tests for friend search and typing status.
   - Implement logic.
