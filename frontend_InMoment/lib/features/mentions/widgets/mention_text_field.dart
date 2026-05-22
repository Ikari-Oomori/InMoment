import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../api/mentions_api.dart';
import '../models/mention_user.dart';
import '../utils/mention_parsing.dart';

class MentionTextField extends StatefulWidget {
  final TextEditingController controller;
  final FocusNode focusNode;
  final bool enabled;
  final int minLines;
  final int maxLines;
  final int? maxLength;
  final String? groupId;
  final TextStyle? style;
  final InputDecoration decoration;
  final ValueChanged<String>? onChanged;

  const MentionTextField({
    super.key,
    required this.controller,
    required this.focusNode,
    required this.enabled,
    required this.minLines,
    required this.maxLines,
    required this.decoration,
    this.maxLength,
    this.groupId,
    this.style,
    this.onChanged,
  });

  @override
  State<MentionTextField> createState() => _MentionTextFieldState();
}

class _MentionTextFieldState extends State<MentionTextField> {
  final MentionsApi _api = MentionsApi();

  Timer? _debounce;
  int _requestId = 0;

  bool _loading = false;
  ActiveMentionMatch? _activeMention;
  ActiveMentionMatch? _lastStableMention;
  List<MentionUser> _suggestions = const [];

  @override
  void initState() {
    super.initState();
    widget.controller.addListener(_handleInputChanged);
    widget.focusNode.addListener(_handleInputChanged);
  }

  @override
  void didUpdateWidget(covariant MentionTextField oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.controller != widget.controller) {
      oldWidget.controller.removeListener(_handleInputChanged);
      widget.controller.addListener(_handleInputChanged);
    }

    if (oldWidget.focusNode != widget.focusNode) {
      oldWidget.focusNode.removeListener(_handleInputChanged);
      widget.focusNode.addListener(_handleInputChanged);
    }
  }

  @override
  void dispose() {
    _debounce?.cancel();
    widget.controller.removeListener(_handleInputChanged);
    widget.focusNode.removeListener(_handleInputChanged);
    super.dispose();
  }

  void _handleInputChanged() {
    widget.onChanged?.call(widget.controller.text);

    if (!widget.enabled || !widget.focusNode.hasFocus) {
      _hideSuggestions(keepLastStableMention: true);
      return;
    }

    final selection = widget.controller.selection;
    if (!selection.isValid || !selection.isCollapsed) {
      _hideSuggestions(keepLastStableMention: true);
      return;
    }

    final active = MentionParsing.findActiveMention(
      widget.controller.text,
      selection.baseOffset,
    );

    if (active == null) {
      _hideSuggestions(keepLastStableMention: true);
      return;
    }

    _activeMention = active;
    _lastStableMention = active;

    final query = active.query.trim();

    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 220), () async {
      final currentRequestId = ++_requestId;

      if (!mounted) return;

      setState(() {
        _loading = true;
      });

      try {
        final result = await _api.searchUsers(
          query: query,
          limit: 5,
          groupId: widget.groupId,
        );

        if (!mounted || currentRequestId != _requestId) return;

        setState(() {
          _loading = false;
          _suggestions = result;
        });
      } catch (_) {
        if (!mounted || currentRequestId != _requestId) return;

        setState(() {
          _loading = false;
          _suggestions = const [];
        });
      }
    });
  }

  void _hideSuggestions({bool keepLastStableMention = false}) {
    _debounce?.cancel();
    if (!mounted) return;

    setState(() {
      _loading = false;
      _activeMention = null;
      _suggestions = const [];
      if (!keepLastStableMention) {
        _lastStableMention = null;
      }
    });
  }

  void _selectSuggestion(MentionUser user) {
    final match = _lastStableMention;

    if (match == null) {
      widget.focusNode.requestFocus();
      return;
    }

    final result = MentionParsing.replaceActiveMention(
      text: widget.controller.text,
      match: match,
      userName: user.userName,
    );

    widget.controller.value = TextEditingValue(
      text: result.text,
      selection: TextSelection.collapsed(offset: result.caretOffset),
      composing: TextRange.empty,
    );

    widget.onChanged?.call(result.text);

    setState(() {
      _loading = false;
      _activeMention = null;
      _lastStableMention = null;
      _suggestions = const [];
    });

    widget.focusNode.requestFocus();
  }

  @override
  Widget build(BuildContext context) {
    final showPanel = widget.enabled &&
        (_loading || _suggestions.isNotEmpty || _activeMention != null);

    return Column(
      mainAxisSize: MainAxisSize.min,
      children: [
        TextField(
          controller: widget.controller,
          focusNode: widget.focusNode,
          enabled: widget.enabled,
          minLines: widget.minLines,
          maxLines: widget.maxLines,
          maxLength: widget.maxLength,
          textInputAction: TextInputAction.newline,
          style: widget.style,
          decoration: widget.decoration,
        ),
        if (showPanel)
          Container(
            margin: const EdgeInsets.only(top: 8),
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: BorderRadius.circular(18),
              border: Border.all(color: AppColors.border),
            ),
            child: _loading
                ? const Padding(
                    padding: EdgeInsets.all(14),
                    child: Row(
                      children: [
                        SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                        SizedBox(width: 10),
                        Text(
                          'Ищем пользователей…',
                          style: TextStyle(
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  )
                : _suggestions.isEmpty
                    ? const Padding(
                        padding: EdgeInsets.all(14),
                        child: Text(
                          'Пользователи не найдены',
                          style: TextStyle(
                            color: AppColors.textSecondary,
                          ),
                        ),
                      )
                    : Column(
                        mainAxisSize: MainAxisSize.min,
                        children: _suggestions.map((user) {
                          return InkWell(
                            onTapDown: (_) => _selectSuggestion(user),
                            onTap: () {},
                            child: Padding(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 12,
                                vertical: 10,
                              ),
                              child: Row(
                                children: [
                                  CircleAvatar(
                                    radius: 18,
                                    backgroundColor: AppColors.accent
                                        .withValues(alpha: 0.22),
                                    backgroundImage:
                                        user.profilePhotoUrl != null &&
                                                user.profilePhotoUrl!
                                                    .trim()
                                                    .isNotEmpty
                                            ? NetworkImage(
                                                user.profilePhotoUrl!,
                                              )
                                            : null,
                                    child: user.profilePhotoUrl == null ||
                                            user.profilePhotoUrl!
                                                .trim()
                                                .isEmpty
                                        ? Text(
                                            user.userName.isNotEmpty
                                                ? user.userName[0]
                                                    .toUpperCase()
                                                : 'U',
                                            style: const TextStyle(
                                              color: AppColors.textPrimary,
                                              fontWeight: FontWeight.w700,
                                            ),
                                          )
                                        : null,
                                  ),
                                  const SizedBox(width: 10),
                                  Expanded(
                                    child: Column(
                                      crossAxisAlignment:
                                          CrossAxisAlignment.start,
                                      children: [
                                        Text(
                                          user.displayName.trim().isEmpty
                                              ? '@${user.userName}'
                                              : user.displayName,
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: const TextStyle(
                                            color: AppColors.textPrimary,
                                            fontWeight: FontWeight.w700,
                                          ),
                                        ),
                                        const SizedBox(height: 2),
                                        Text(
                                          '@${user.userName}',
                                          maxLines: 1,
                                          overflow: TextOverflow.ellipsis,
                                          style: const TextStyle(
                                            color: AppColors.textSecondary,
                                            fontSize: 12,
                                          ),
                                        ),
                                      ],
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          );
                        }).toList(),
                      ),
          ),
      ],
    );
  }
}