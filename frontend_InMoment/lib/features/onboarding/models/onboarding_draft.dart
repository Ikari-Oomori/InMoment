class OnboardingDraft {
  final String email;
  final String password;
  final String firstName;
  final String lastName;
  final String userName;
  final bool contactsEnabled;
  final List<OnboardingContactPreview> importedContacts;

  const OnboardingDraft({
    this.email = '',
    this.password = '',
    this.firstName = '',
    this.lastName = '',
    this.userName = '',
    this.contactsEnabled = false,
    this.importedContacts = const [],
  });

  String get fullName => '$firstName $lastName'.trim();

  OnboardingDraft copyWith({
    String? email,
    String? password,
    String? firstName,
    String? lastName,
    String? userName,
    bool? contactsEnabled,
    List<OnboardingContactPreview>? importedContacts,
  }) {
    return OnboardingDraft(
      email: email ?? this.email,
      password: password ?? this.password,
      firstName: firstName ?? this.firstName,
      lastName: lastName ?? this.lastName,
      userName: userName ?? this.userName,
      contactsEnabled: contactsEnabled ?? this.contactsEnabled,
      importedContacts: importedContacts ?? this.importedContacts,
    );
  }
}

class OnboardingContactPreview {
  final String displayName;
  final String? phone;
  final String? email;

  const OnboardingContactPreview({
    required this.displayName,
    this.phone,
    this.email,
  });
}