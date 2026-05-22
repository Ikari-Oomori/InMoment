class AuthDraft {
  final String email;

  const AuthDraft({
    this.email = '',
  });

  AuthDraft copyWith({
    String? email,
  }) {
    return AuthDraft(
      email: email ?? this.email,
    );
  }
}