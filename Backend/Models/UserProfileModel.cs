// Standardized to production level
// Purpose: MVVM model representing the current user's editable profile
// Dependencies: None (pure INPC model)

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Hermes.Models
{
    public class UserProfileModel : INotifyPropertyChanged
    {
        private string _userId = string.Empty;
        private string _email = string.Empty;
        private string _username = string.Empty;
        private string _displayName = string.Empty;
        private string _bio = string.Empty;
        private string _avatarUrl = string.Empty;
        private bool _isEmailVerified;
        private DateTime _createdAt;
        private bool _isOnline;

        public string UserId
        {
            get => _userId;
            set { _userId = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string Email
        {
            get => _email;
            set { _email = value ?? string.Empty; OnPropertyChanged(); }
        }

        /// <summary>Login handle (immutable after registration, shown read-only).</summary>
        public string Username
        {
            get => _username;
            set { _username = value ?? string.Empty; OnPropertyChanged(); }
        }

        /// <summary>Display name — user can freely edit this.</summary>
        public string DisplayName
        {
            get => _displayName;
            set { _displayName = value ?? string.Empty; OnPropertyChanged(); }
        }

        public string Bio
        {
            get => _bio;
            set { _bio = value ?? string.Empty; OnPropertyChanged(); }
        }

        /// <summary>Local file path OR remote URL (Firebase Storage).</summary>
        public string AvatarUrl
        {
            get => _avatarUrl;
            set { _avatarUrl = value ?? string.Empty; OnPropertyChanged(); OnPropertyChanged(nameof(HasAvatar)); }
        }

        public bool HasAvatar => !string.IsNullOrEmpty(_avatarUrl);

        public bool IsEmailVerified
        {
            get => _isEmailVerified;
            set { _isEmailVerified = value; OnPropertyChanged(); OnPropertyChanged(nameof(EmailVerifiedText)); }
        }

        public string EmailVerifiedText => _isEmailVerified ? "✅ Đã xác thực Email" : "⚠️ Chưa xác thực Email";

        public DateTime CreatedAt
        {
            get => _createdAt;
            set { _createdAt = value; OnPropertyChanged(); OnPropertyChanged(nameof(CreatedAtText)); }
        }

        public string CreatedAtText => _createdAt == default ? "—" : _createdAt.ToString("dd/MM/yyyy");

        public bool IsOnline
        {
            get => _isOnline;
            set { _isOnline = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? name = null)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
