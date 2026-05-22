import 'package:flutter/foundation.dart';

import '../models/onboarding_draft.dart';

class OnboardingFlowController extends ChangeNotifier {
  OnboardingDraft _draft = const OnboardingDraft();

  OnboardingDraft get draft => _draft;

  void update(OnboardingDraft next) {
    _draft = next;
    notifyListeners();
  }

  void updateEmail(String value) {
    _draft = _draft.copyWith(email: value);
    notifyListeners();
  }

  void updatePassword(String value) {
    _draft = _draft.copyWith(password: value);
    notifyListeners();
  }

  void updateName({
    required String firstName,
    required String lastName,
  }) {
    _draft = _draft.copyWith(
      firstName: firstName,
      lastName: lastName,
    );
    notifyListeners();
  }

  void updateUserName(String value) {
    _draft = _draft.copyWith(userName: value);
    notifyListeners();
  }

  void updateContacts({
    required bool enabled,
    required List<OnboardingContactPreview> importedContacts,
  }) {
    _draft = _draft.copyWith(
      contactsEnabled: enabled,
      importedContacts: importedContacts,
    );
    notifyListeners();
  }
}