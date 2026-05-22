import 'dart:async';

import 'package:flutter/material.dart';
import 'package:share_plus/share_plus.dart';

import '../api/photo_api.dart';
import '../models/photo_details.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../../../core/widgets/inmoment_dialog_wrapper.dart';
import '../../contacts/api/search_api.dart';
import '../../comments/api/comments_api.dart';
import '../../comments/models/comment_item.dart';
import '../../groups/controllers/active_group_controller.dart';
import '../../profile/api/profile_api.dart';
import '../../profile/models/user_profile.dart';
import '../../profile/pages/public_user_profile_page.dart';
import '../../blocks/api/blocks_api.dart';
import '../../reports/models/report_reason_option.dart';
import '../../reports/pages/create_report_page.dart';
import '../../mentions/api/mentions_api.dart';
import '../../mentions/models/mention_user.dart';
import '../../mentions/widgets/mention_text.dart';
import '../../mentions/widgets/mention_text_field.dart';
import '../../reactions/api/reactions_api.dart';
import '../../reactions/models/reaction_catalog.dart';
import '../../reactions/models/reaction_summary_utils.dart';
import '../../reactions/widgets/reaction_counter_pill.dart';
import '../../reactions/widgets/reaction_picker_sheet.dart';
import '../../gifs/widgets/gif_picker_sheet.dart';
import '../../widget/services/widget_image_cache_service_io.dart';
import '../../widget/services/widget_sync_service.dart';
import '../../../core/widgets/inmoment_responsive_content.dart';
import '../../../core/layout/inmoment_media_frame.dart';

class _PhotoDetailsCacheEntry {
  final PhotoDetails photo;
  final List<CommentItem> comments;
  final UserProfile me;
  final DateTime savedAt;

  const _PhotoDetailsCacheEntry({
    required this.photo,
    required this.comments,
    required this.me,
    required this.savedAt,
  });

  bool get isFresh {
    return DateTime.now().difference(savedAt) < const Duration(minutes: 3);
  }
}

class _ProfileMemoryCache {
  static UserProfile? _value;
  static DateTime? _savedAt;
  static Future<UserProfile>? _inFlight;

  static bool get _isFresh {
    final savedAt = _savedAt;
    if (_value == null || savedAt == null) return false;

    return DateTime.now().difference(savedAt) < const Duration(minutes: 5);
  }

  static Future<UserProfile> get(
    ProfileApi api, {
    bool force = false,
  }) {
    if (!force && _value != null && _isFresh) {
      return Future.value(_value);
    }

    final existing = _inFlight;
    if (!force && existing != null) {
      return existing;
    }

    final future = api.getMe().then((profile) {
      _value = profile;
      _savedAt = DateTime.now();
      return profile;
    }).whenComplete(() {
      _inFlight = null;
    });

    _inFlight = future;
    return future;
  }
}

class PhotoDetailsPage extends StatefulWidget {
  final String photoId;
  final String? groupId;
  final String? initialCommentId;

  const PhotoDetailsPage({
    super.key,
    required this.photoId,
    this.groupId,
    this.initialCommentId,
  });

  @override
  State<PhotoDetailsPage> createState() => _PhotoDetailsPageState();
}

class _PhotoDetailsPageState extends State<PhotoDetailsPage> {
  static final Map<String, _PhotoDetailsCacheEntry> _memoryCache =
      <String, _PhotoDetailsCacheEntry>{};

  final PhotoApi _photoApi = PhotoApi();
  final CommentsApi _commentsApi = CommentsApi();
  final ReactionsApi _reactionsApi = ReactionsApi();
  final ProfileApi _profileApi = ProfileApi();
  final BlocksApi _blocksApi = BlocksApi();
  final MentionsApi _mentionsApi = MentionsApi();
  final _groupController = ActiveGroupController.instance;
  final SearchApi _searchApi = SearchApi();

  final TextEditingController _commentController = TextEditingController();
  final FocusNode _commentFocusNode = FocusNode();

  final Set<String> _updatingCommentReactionIds = <String>{};
  final Set<String> _editingCommentIds = <String>{};
  final Set<String> _deletingCommentIds = <String>{};

  final ScrollController _scrollController = ScrollController();
  final Map<String, GlobalKey> _commentKeys = <String, GlobalKey>{};
  Timer? _commentHighlightTimer;

  bool _loading = true;
  bool _submittingComment = false;
  bool _updatingReaction = false;
  bool _deletingPhoto = false;
  String? _error;
  PhotoDetailsFailureType? _failureType;

  String? _currentUserId;
  PhotoDetails? _photo;
  List<CommentItem> _comments = const [];
  CommentItem? _replyTo;
  String? _selectedGifUrl;
  String? _openedFromCommentId;
  String? _highlightedCommentId;

  String get _cacheKey {
    final groupPart = widget.groupId?.trim() ?? '';
    return '$groupPart:${widget.photoId}';
  }

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _commentHighlightTimer?.cancel();
    _scrollController.dispose();
    _commentController.dispose();
    _commentFocusNode.dispose();
    super.dispose();
  }

  bool get _isAuthor {
    final photo = _photo;
    final me = _currentUserId?.trim();

    if (photo == null) return false;
    if (me == null || me.isEmpty) return false;

    return photo.authorId.trim() == me;
  }

  bool get _isGroupManager {
    final groupId = widget.groupId ?? _photo?.groupId;
    if (groupId == null || groupId.isEmpty) return false;

    final groups = _groupController.groups;
    for (final g in groups) {
      if (g.id == groupId) {
        return g.isManager;
      }
    }

    return false;
  }

  bool get _canDelete {
    final photo = _photo;
    if (photo != null) {
      return photo.canDelete;
    }

    return _isAuthor || _isGroupManager;
  }

  bool get _canEdit {
    final photo = _photo;
    if (photo != null) {
      return photo.canEdit;
    }

    return _isAuthor;
  }

  bool _applyCachedSnapshot() {
    final cached = _memoryCache[_cacheKey];

    if (cached == null || !cached.isFresh) {
      return false;
    }

    if (!mounted) return false;

    _syncCommentKeys(cached.comments);

    setState(() {
      _photo = cached.photo;
      _comments = cached.comments;
      _currentUserId = cached.me.id;
      _loading = false;
      _error = null;
      _failureType = null;
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _precachePhotoMedia(cached.photo);
    });

    return true;
  }

  void _syncCommentKeys(List<CommentItem> comments) {
    for (final item in comments) {
      _commentKeys.putIfAbsent(item.id, () => GlobalKey());
    }

    final existingIds = comments.map((e) => e.id).toSet();
    _commentKeys.removeWhere((key, value) => !existingIds.contains(key));
  }

  void _storeSnapshot({
    required PhotoDetails photo,
    required List<CommentItem> comments,
    required UserProfile me,
  }) {
    _memoryCache[_cacheKey] = _PhotoDetailsCacheEntry(
      photo: photo,
      comments: comments,
      me: me,
      savedAt: DateTime.now(),
    );

    if (_memoryCache.length > 80) {
      final keys = _memoryCache.keys.take(_memoryCache.length - 80).toList();
      for (final key in keys) {
        _memoryCache.remove(key);
      }
    }
  }

  void _precachePhotoMedia(PhotoDetails photo) {
    final url = photo.url.trim();
    final isImage = photo.contentType.toLowerCase().startsWith('image/');

    if (url.isNotEmpty && isImage) {
      precacheImage(NetworkImage(url), context);
    }

    final avatarUrl = photo.authorProfilePhotoUrl?.trim();
    if (avatarUrl != null && avatarUrl.isNotEmpty) {
      precacheImage(NetworkImage(avatarUrl), context);
    }
  }

  Future<void> _load({
    bool silent = false,
    bool force = false,
  }) async {
    final hadCachedSnapshot = !force && _applyCachedSnapshot();

    if (!silent && !hadCachedSnapshot) {
      setState(() {
        _loading = true;
        _error = null;
        _failureType = null;
      });
    } else {
      setState(() {
        _error = null;
        _failureType = null;
      });
    }

    try {
      final results = await Future.wait<Object>([
        _photoApi.getPhotoDetails(
          widget.photoId,
          groupId: widget.groupId,
        ),
        _commentsApi.getComments(widget.photoId),
        _ProfileMemoryCache.get(
          _profileApi,
          force: force && _currentUserId == null,
        ),
      ]);

      final photo = results[0] as PhotoDetails;
      final comments = results[1] as List<CommentItem>;
      final me = results[2] as UserProfile;

      final initialCommentId = widget.initialCommentId?.trim();
      final shouldOpenComment = initialCommentId != null &&
          initialCommentId.isNotEmpty &&
          _openedFromCommentId != initialCommentId &&
          comments.any((c) => c.id == initialCommentId);

      _storeSnapshot(
        photo: photo,
        comments: comments,
        me: me,
      );

      if (!mounted) return;

      _syncCommentKeys(comments);

      setState(() {
        _photo = photo;
        _comments = comments;
        _currentUserId = me.id;
        _loading = false;
        _error = null;
        _failureType = null;
      });

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;
        _precachePhotoMedia(photo);
      });

      if (shouldOpenComment) {
        _openedFromCommentId = initialCommentId;

        WidgetsBinding.instance.addPostFrameCallback((_) async {
          if (!mounted) return;

          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(
              content: Text('Открыт комментарий из уведомления'),
              duration: Duration(seconds: 2),
            ),
          );

          await _focusCommentById(initialCommentId);
        });
      }
    } on PhotoDetailsFailure catch (e) {
      if (!mounted) return;

      if (hadCachedSnapshot) {
        _showSnack('Не удалось обновить публикацию: ${e.message}');
        return;
      }

      setState(() {
        _loading = false;
        _failureType = e.type;
        _error = e.message;
      });
    } catch (e) {
      if (!mounted) return;

      if (hadCachedSnapshot) {
        _showSnack('Не удалось обновить публикацию: ${_normalizeError(e)}');
        return;
      }

      setState(() {
        _loading = false;
        _failureType = PhotoDetailsFailureType.unknown;
        _error = _normalizeError(e);
      });
    }
  }

  Future<void> _openCreateReportPage({
    required ReportTargetType targetType,
    required String targetId,
    required String titleText,
    required String subtitleText,
  }) async {
    final created = await Navigator.of(context).push<bool>(
      MaterialPageRoute(
        builder: (_) => CreateReportPage(
          targetType: targetType,
          targetId: targetId,
          titleText: titleText,
          subtitleText: subtitleText,
        ),
      ),
    );

    if (!mounted) return;

    if (created == true) {
      _showSnack('Жалоба отправлена');
    }
  }

  Future<MentionUser?> _resolveMentionUser(String userName) async {
    final safeUserName = userName.trim();
    if (safeUserName.isEmpty) return null;

    final groupId = widget.groupId ?? _photo?.groupId;

    try {
      final matches = await _mentionsApi.searchUsers(
        query: safeUserName,
        limit: 10,
        groupId: groupId,
      );

      for (final item in matches) {
        if (item.userName.toLowerCase() == safeUserName.toLowerCase()) {
          return item;
        }
      }
    } catch (_) {
      // fallback ниже
    }

    try {
      final globalMatches = await _searchApi.searchUsers(
        safeUserName,
        limit: 15,
      );

      for (final item in globalMatches) {
        if (item.userName.toLowerCase() == safeUserName.toLowerCase()) {
          return MentionUser(
            id: item.id,
            userName: item.userName,
            displayName: item.displayName,
            profilePhotoUrl: item.profilePhotoUrl,
          );
        }
      }
    } catch (_) {
      // silent fallback
    }

    return null;
  }

  Future<void> _openMentionByUserName(String userName) async {
    try {
      final resolved = await _resolveMentionUser(userName);

      if (!mounted) return;

      if (resolved == null) {
        _showSnack('Не удалось открыть профиль по упоминанию.');
        return;
      }

      await Navigator.of(context).push(
        MaterialPageRoute(
          builder: (_) => PublicUserProfilePage(
            userId: resolved.id,
          ),
        ),
      );
    } catch (e) {
      if (!mounted) return;
      _showSnack('Не удалось открыть профиль: ${_normalizeError(e)}');
    }
  }

  Future<void> _reportPhoto() async {
    final photo = _photo;
    if (photo == null) return;

    await _openCreateReportPage(
      targetType: ReportTargetType.photo,
      targetId: photo.id,
      titleText: 'Пожаловаться на публикацию',
      subtitleText:
          'Опишите, почему эта публикация нарушает правила или кажется вам нежелательной.',
    );
  }

    Future<void> _reportPhotoAuthor() async {
    final photo = _photo;
    if (photo == null) return;

    if (_currentUserId != null && photo.authorId == _currentUserId) {
      _showSnack('Нельзя пожаловаться на самого себя.');
      return;
    }

    await _openCreateReportPage(
      targetType: ReportTargetType.user,
      targetId: photo.authorId,
      titleText: 'Пожаловаться на пользователя',
      subtitleText:
          'Жалоба будет отправлена на пользователя — автора этой публикации.',
    );
  }

  Future<void> _reportComment(CommentItem item) async {
    await _openCreateReportPage(
      targetType: ReportTargetType.comment,
      targetId: item.id,
      titleText: 'Пожаловаться на комментарий',
      subtitleText:
          'Жалоба будет отправлена на проверку. Выберите причину и при необходимости добавьте описание.',
    );
  }

  Future<void> _reportCommentAuthor(CommentItem item) async {
    if (item.userId.trim().isEmpty) return;

    if (_currentUserId != null && item.userId == _currentUserId) {
      _showSnack('Нельзя пожаловаться на самого себя.');
      return;
    }

    await _openCreateReportPage(
      targetType: ReportTargetType.user,
      targetId: item.userId,
      titleText: 'Пожаловаться на пользователя',
      subtitleText:
          'Жалоба будет отправлена на автора этого комментария.',
    );
  }

  Future<void> _blockPhotoAuthor() async {
    final photo = _photo;
    if (photo == null) return;
    if (photo.authorId.trim().isEmpty) return;

    if (_currentUserId != null && photo.authorId == _currentUserId) {
      _showSnack('Нельзя заблокировать самого себя.');
      return;
    }

    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) {
            return InMomentDialogWrapper(
              child: AlertDialog(
                backgroundColor: AppColors.card,
                title: const Text(
                  'Заблокировать пользователя?',
                  style: TextStyle(color: AppColors.textPrimary),
                ),
                content: Text(
                  'Пользователь ${_displayName(photo)} будет добавлен в список блокировок.',
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    height: 1.4,
                  ),
                ),
                actions: [
                  TextButton(
                    onPressed: () => Navigator.of(context).pop(false),
                    child: const Text('Отмена'),
                  ),
                  FilledButton(
                    onPressed: () => Navigator.of(context).pop(true),
                    child: const Text('Заблокировать'),
                  ),
                ],
              ),
            );
          },
        ) ??
        false;

    if (!confirmed) return;

    try {
      await _blocksApi.blockUser(photo.authorId);
      if (!mounted) return;
      _showSnack('Пользователь заблокирован');
    } catch (e) {
      if (!mounted) return;
      _showSnack('Не удалось заблокировать: ${_normalizeError(e)}');
    }
  }

  Future<void> _blockCommentAuthor(CommentItem item) async {
    if (item.userId.trim().isEmpty) return;

    if (_currentUserId != null && item.userId == _currentUserId) {
      _showSnack('Нельзя заблокировать самого себя.');
      return;
    }

    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) {
            return InMomentDialogWrapper(
              child: AlertDialog(
              backgroundColor: AppColors.card,
              title: const Text(
                'Заблокировать пользователя?',
                style: TextStyle(color: AppColors.textPrimary),
              ),
              content: Text(
                'Пользователь @${item.userName} будет добавлен в список блокировок.',
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  height: 1.4,
                ),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text('Отмена'),
                ),
                FilledButton(
                  onPressed: () => Navigator.of(context).pop(true),
                  child: const Text('Заблокировать'),
                ),
              ],
              ),
            );
          },
        ) ??
        false;

    if (!confirmed) return;

    try {
      await _blocksApi.blockUser(item.userId);
      if (!mounted) return;
      _showSnack('Пользователь заблокирован');
    } catch (e) {
      if (!mounted) return;
      _showSnack('Не удалось заблокировать: ${_normalizeError(e)}');
    }
  }

 Future<void> _submitComment() async {
    if (_submittingComment) return;

    final text = _commentController.text.trim();
    final gifUrl = _selectedGifUrl?.trim();

    if (text.isEmpty && (gifUrl == null || gifUrl.isEmpty)) {
      _commentFocusNode.requestFocus();
      return;
    }

    final replyTarget = _replyTo;

    setState(() {
      _submittingComment = true;
    });

    try {
      if (replyTarget == null) {
        await _commentsApi.createComment(
          photoId: widget.photoId,
          text: text,
          gifUrl: gifUrl,
        );
      } else {
        await _commentsApi.replyToComment(
          photoId: widget.photoId,
          parentCommentId: replyTarget.id,
          text: text,
          gifUrl: gifUrl,
        );
      }

      if (!mounted) return;

      _commentController.clear();
      _commentFocusNode.unfocus();

      setState(() {
        _replyTo = null;
        _selectedGifUrl = null;
      });

      await _load(silent: true);

      if (!mounted) return;

      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted) return;

        final lastCommentId = _comments.isNotEmpty ? _comments.last.id : null;
        if (lastCommentId != null) {
          _focusCommentById(lastCommentId);
        }
      });
    } catch (e) {
      if (!mounted) return;

      _showSnack(
        'Не удалось отправить комментарий: ${_normalizeError(e)}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _submittingComment = false;
        });
      }
    }
  }

  Future<void> _deletePhoto() async {
    if (_deletingPhoto) return;

    final groupId = widget.groupId ?? _photo?.groupId;
    if (groupId == null || groupId.trim().isEmpty) {
      _showSnack('Не удалось определить группу публикации.');
      return;
    }

    final confirmed = await showDialog<bool>(
          context: context,
          builder: (context) {
            return InMomentDialogWrapper(
              child: AlertDialog(
              backgroundColor: AppColors.card,
              title: const Text(
                'Удалить публикацию?',
                style: TextStyle(color: AppColors.textPrimary),
              ),
              content: const Text(
                'Фото будет удалено без возможности восстановления.',
                style: TextStyle(color: AppColors.textSecondary),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(context).pop(false),
                  child: const Text('Отмена'),
                ),
                FilledButton(
                  onPressed: () => Navigator.of(context).pop(true),
                  child: const Text('Удалить'),
                ),
              ],
              ),
            );
          },
        ) ??
        false;

    if (!confirmed) return;
    if (_deletingPhoto) return;

    setState(() {
      _deletingPhoto = true;
    });

    try {
      await _photoApi.deletePhoto(
        groupId: groupId,
        photoId: widget.photoId,
      );

      await WidgetImageCacheService.instance.clearPreviewCache();
      await WidgetSyncService.instance.syncFromBackend();

      if (!mounted) return;

      Navigator.of(context).pop(true);
    } catch (e) {
      if (!mounted) return;

      _showSnack(
        'Не удалось удалить: ${_normalizeError(e)}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _deletingPhoto = false;
        });
      }
    }
  }

  Future<void> _editComment(CommentItem item) async {
    if (_editingCommentIds.contains(item.id)) return;

    final controller = TextEditingController(text: item.text);
    final focusNode = FocusNode();

    final updatedText = await showDialog<String>(
      context: context,
      builder: (dialogContext) {
        return InMomentDialogWrapper(
          child: AlertDialog(
          backgroundColor: AppColors.card,
          title: const Text(
            'Редактировать комментарий',
            style: TextStyle(color: AppColors.textPrimary),
          ),
          content: MentionTextField(
            controller: controller,
            focusNode: focusNode,
            enabled: true,
            minLines: 3,
            maxLines: 6,
            maxLength: 500,
            groupId: widget.groupId ?? _photo?.groupId,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: InputDecoration(
              hintText: 'Введите текст комментария',
              hintStyle: const TextStyle(color: AppColors.textSecondary),
              filled: true,
              fillColor: AppColors.surface,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
                borderSide: BorderSide.none,
              ),
              counterText: '',
            ),
            onChanged: (_) {},
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Отмена'),
            ),
            FilledButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(controller.text.trim());
              },
              child: const Text('Сохранить'),
            ),
          ],
          ),
        );
      },
    );

    controller.dispose();
    focusNode.dispose();

    if (!mounted) return;
    if (updatedText == null) return;

    final trimmed = updatedText.trim();

    if (trimmed.isEmpty) {
      _showSnack('Комментарий не может быть пустым');
      return;
    }

    if (trimmed == item.text.trim()) return;
    if (_editingCommentIds.contains(item.id)) return;

    setState(() {
      _editingCommentIds.add(item.id);
    });

    try {
      await _commentsApi.editComment(
        commentId: item.id,
        text: trimmed,
      );

      if (!mounted) return;

      _showSnack('Комментарий обновлён');
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      _showSnack(
        'Не удалось обновить комментарий: ${_normalizeError(e)}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _editingCommentIds.remove(item.id);
        });
      }
    }
  }

  Future<void> _deleteComment(CommentItem item) async {
    if (_deletingCommentIds.contains(item.id)) return;

    final confirmed = await showDialog<bool>(
          context: context,
          builder: (dialogContext) {
            return InMomentDialogWrapper(
              child: AlertDialog(
              backgroundColor: AppColors.card,
              title: const Text(
                'Удалить комментарий?',
                style: TextStyle(color: AppColors.textPrimary),
              ),
              content: const Text(
                'Комментарий будет удалён без возможности восстановления.',
                style: TextStyle(color: AppColors.textSecondary),
              ),
              actions: [
                TextButton(
                  onPressed: () => Navigator.of(dialogContext).pop(false),
                  child: const Text('Отмена'),
                ),
                FilledButton(
                  onPressed: () => Navigator.of(dialogContext).pop(true),
                  child: const Text('Удалить'),
                ),
              ],
              ),
            );
          },
        ) ??
        false;

    if (!confirmed) return;
    if (_deletingCommentIds.contains(item.id)) return;

    setState(() {
      _deletingCommentIds.add(item.id);
    });

    try {
      await _commentsApi.deleteComment(commentId: item.id);

      if (!mounted) return;

      _showSnack('Комментарий удалён');
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      _showSnack(
        'Не удалось удалить комментарий: ${_normalizeError(e)}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _deletingCommentIds.remove(item.id);
        });
      }
    }
  }

  Future<void> _onReactionTap(ReactionCatalogItem option) async {
    if (_updatingReaction || _photo == null) return;

    final current = _photo!.myReaction;

    setState(() {
      _updatingReaction = true;
    });

    try {
      if (current == option.type) {
        await _reactionsApi.removeReaction(photoId: widget.photoId);
      } else {
        await _reactionsApi.setReaction(
          photoId: widget.photoId,
          type: option.type,
        );
      }

      if (!mounted) return;
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      _showSnack(
        'Не удалось обновить реакцию: ${_normalizeError(e)}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _updatingReaction = false;
        });
      }
    }
  }

  Future<void> _showPhotoReactionPickerAt(Offset position) async {
    final selected = await showReactionPopupMenu(
      context,
      position: position,
      selectedType: _photo?.myReaction ?? 0,
    );

    if (selected != null) {
      await _onReactionTap(selected);
    }
  }

  void _startReply(CommentItem item) {
    final prefix = '@${item.userName} ';

    if (_commentController.text.trim().isEmpty) {
      _commentController.value = TextEditingValue(
        text: prefix,
        selection: TextSelection.collapsed(offset: prefix.length),
      );
    }

    setState(() {
      _replyTo = item;
    });

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;

      _commentFocusNode.requestFocus();

      if (_scrollController.hasClients) {
        _scrollController.animateTo(
          _scrollController.position.maxScrollExtent,
          duration: const Duration(milliseconds: 260),
          curve: Curves.easeOut,
        );
      }
    });
  }
  void _focusComposer() {
    _commentFocusNode.requestFocus();
  }

  void _insertIntoComment(String value) {
    final text = _commentController.text;
    final selection = _commentController.selection;

    final start = selection.start < 0 ? text.length : selection.start;
    final end = selection.end < 0 ? text.length : selection.end;

    final updated = text.replaceRange(start, end, value);
    final offset = start + value.length;

    _commentController.value = TextEditingValue(
      text: updated,
      selection: TextSelection.collapsed(offset: offset),
    );
  }

 Future<void> _showGifPickerSheet() async {
    final selected = await showGifPickerSheet(context);

    if (!mounted) return;
    if (selected == null || selected.trim().isEmpty) return;

    setState(() {
      _selectedGifUrl = selected.trim();
    });
  }

  Future<void> _focusCommentById(String commentId) async {
    final key = _commentKeys[commentId];
    final context = key?.currentContext;

    if (context == null) return;

    await Scrollable.ensureVisible(
      context,
      duration: const Duration(milliseconds: 420),
      curve: Curves.easeInOut,
      alignment: 0.18,
    );

    if (!mounted) return;

    setState(() {
      _highlightedCommentId = commentId;
    });

    _commentHighlightTimer?.cancel();
    _commentHighlightTimer = Timer(const Duration(seconds: 2), () {
      if (!mounted) return;
      setState(() {
        if (_highlightedCommentId == commentId) {
          _highlightedCommentId = null;
        }
      });
    });
  }

  void _cancelReply() {
    setState(() {
      _replyTo = null;
    });

    if (_commentController.text.trim().startsWith('@')) {
      final text = _commentController.text.trim();
      final firstSpace = text.indexOf(' ');

      if (firstSpace > 0 && firstSpace < text.length - 1) {
        final rest = text.substring(firstSpace + 1);
        _commentController.value = TextEditingValue(
          text: rest,
          selection: TextSelection.collapsed(offset: rest.length),
        );
      } else {
        _commentController.clear();
      }
    }

    _commentFocusNode.requestFocus();
  }

    void _sharePhoto() {
      const text = '''
  Посмотри фото в InMoment 📸

  Фото доступно только участникам группы. Открой приложение InMoment и выбери нужную группу.
  ''';

      Share.share(text.trim());
    }

 void _showPhotoMenu() {
    final canDelete = _canDelete;
    final canEdit = _canEdit;

    showModalBottomSheet<void>(
      context: context,
      backgroundColor: Colors.transparent,
      builder: (sheetContext) {
        return SafeArea(
          top: false,
          child: Center(
            child: SizedBox(
              width: InMomentMediaFrame.resolveBottomSheetWidth(
                MediaQuery.sizeOf(sheetContext).width,
              ),
              child: Container(
                margin: const EdgeInsets.fromLTRB(12, 0, 12, 12),
                padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
            decoration: BoxDecoration(
              color: AppColors.card,
              borderRadius: BorderRadius.circular(28),
              border: Border.all(color: AppColors.border),
            ),
            child: SingleChildScrollView(
              physics: const ClampingScrollPhysics(),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  Container(
                    width: 42,
                    height: 4,
                    decoration: BoxDecoration(
                      color: AppColors.textSecondary.withValues(alpha: 0.35),
                      borderRadius: BorderRadius.circular(999),
                    ),
                  ),
                  const SizedBox(height: 14),
                  ListTile(
                    leading: const Icon(
                      Icons.ios_share_rounded,
                      color: AppColors.textPrimary,
                    ),
                    title: const Text(
                      'Поделиться',
                      style: TextStyle(color: AppColors.textPrimary),
                    ),
                    onTap: () {
                      Navigator.of(sheetContext).pop();
                      _sharePhoto();
                    },
                  ),
                  if (!_isAuthor)
                    ListTile(
                      leading: const Icon(
                        Icons.flag_outlined,
                        color: AppColors.textPrimary,
                      ),
                      title: const Text(
                        'Пожаловаться',
                        style: TextStyle(color: AppColors.textPrimary),
                      ),
                      onTap: () {
                        Navigator.of(sheetContext).pop();
                        _reportPhoto();
                      },
                    ),
                  if (!_isAuthor)
                    ListTile(
                      leading: const Icon(
                        Icons.person_off_outlined,
                        color: AppColors.textPrimary,
                      ),
                      title: const Text(
                        'Пожаловаться на автора',
                        style: TextStyle(color: AppColors.textPrimary),
                      ),
                      onTap: () {
                        Navigator.of(sheetContext).pop();
                        _reportPhotoAuthor();
                      },
                    ),  
                  if (!_isAuthor)
                    ListTile(
                      leading: const Icon(
                        Icons.block_outlined,
                        color: Colors.redAccent,
                      ),
                      title: const Text(
                        'Заблокировать автора',
                        style: TextStyle(color: Colors.redAccent),
                      ),
                      onTap: () {
                        Navigator.of(sheetContext).pop();
                        _blockPhotoAuthor();
                      },
                    ),
                  if (canEdit)
                    ListTile(
                      leading: const Icon(
                        Icons.edit_outlined,
                        color: AppColors.textPrimary,
                      ),
                      title: const Text(
                        'Редактировать',
                        style: TextStyle(color: AppColors.textPrimary),
                      ),
                      onTap: () {
                        Navigator.of(sheetContext).pop();
                        _editPhoto();
                      },
                    ),
                  if (canDelete)
                    ListTile(
                      enabled: !_deletingPhoto,
                      leading: const Icon(
                        Icons.delete_outline_rounded,
                        color: Colors.redAccent,
                      ),
                      title: Text(
                        _deletingPhoto ? 'Удаляем…' : 'Удалить',
                        style: const TextStyle(color: Colors.redAccent),
                      ),
                      onTap: _deletingPhoto
                          ? null
                          : () {
                              Navigator.of(sheetContext).pop();
                              _deletePhoto();
                          }
                   ),
                ],
              ) 
              ),
            ),
          ),
        ),
      );
      }
    );
  }


  void _showSnack(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

    Future<void> _editPhoto() async {
    final photo = _photo;
    final groupId = widget.groupId ?? _photo?.groupId;

    if (photo == null) return;

    if (groupId == null || groupId.trim().isEmpty) {
      _showSnack('Не удалось определить группу публикации.');
      return;
    }

    final controller = TextEditingController(text: photo.caption ?? '');
    final focusNode = FocusNode();

    final updatedCaption = await showDialog<String>(
      context: context,
      builder: (dialogContext) {
        return InMomentDialogWrapper(
          child: AlertDialog(
          backgroundColor: AppColors.card,
          title: const Text(
            'Редактировать подпись',
            style: TextStyle(color: AppColors.textPrimary),
          ),
          content: MentionTextField(
            controller: controller,
            focusNode: focusNode,
            enabled: true,
            minLines: 3,
            maxLines: 6,
            maxLength: 500,
            groupId: groupId,
            style: const TextStyle(color: AppColors.textPrimary),
            decoration: InputDecoration(
              hintText: 'Добавьте подпись к публикации',
              hintStyle: const TextStyle(color: AppColors.textSecondary),
              helperText: 'До 500 символов. Можно оставить пусто.',
              helperStyle: const TextStyle(color: AppColors.textSecondary),
              filled: true,
              fillColor: AppColors.surface,
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(16),
                borderSide: BorderSide.none,
              ),
              counterText: '',
            ),
            onChanged: (_) {},
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(dialogContext).pop(),
              child: const Text('Отмена'),
            ),
            FilledButton(
              onPressed: () {
                Navigator.of(dialogContext).pop(controller.text);
              },
              child: const Text('Сохранить'),
            ),
          ],
          ),
        );
      },
    );

    focusNode.dispose();
    controller.dispose();

    if (!mounted) return;
    if (updatedCaption == null) return;

    final normalized = updatedCaption.trim();
    final current = (photo.caption ?? '').trim();

    if (normalized == current) {
      return;
    }

    try {
      await _photoApi.updatePhotoCaption(
        groupId: groupId,
        photoId: widget.photoId,
        caption: normalized.isEmpty ? null : normalized,
      );

      if (!mounted) return;

      _showSnack('Публикация обновлена');
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;

      _showSnack(
        'Не удалось обновить публикацию: ${_normalizeError(e)}',
      );
    }
  }

  String _normalizeError(Object error) {
    final raw = error.toString().trim();
    const prefix = 'Exception: ';
    if (raw.startsWith(prefix)) {
      return raw.substring(prefix.length).trim();
    }
    return raw;
  }

  String _formatDate(DateTime dateTime) {
    final local = dateTime.toLocal();

    String two(int n) => n.toString().padLeft(2, '0');

    return '${two(local.day)}.${two(local.month)}.${local.year} ${two(local.hour)}:${two(local.minute)}';
  }

  String _displayName(PhotoDetails photo) {
    if (!photo.authorIsActive) {
      return 'Деактивированный пользователь';
    }

    final first = (photo.authorFirstName ?? '').trim();
    final last = (photo.authorLastName ?? '').trim();
    final joined = '$first $last'.trim();

    if (joined.isNotEmpty) return joined;
    if (photo.authorUserName.trim().isNotEmpty) {
      return '@${photo.authorUserName.trim()}';
    }

    return 'Пользователь';
  }

  String _commentDisplayName(CommentItem item) {
    if (!item.userIsActive) {
      return 'Деактивированный пользователь';
    }

    final first = (item.firstName ?? '').trim();
    final last = (item.lastName ?? '').trim();
    final joined = '$first $last'.trim();

    if (joined.isNotEmpty) return joined;
    if (item.userName.trim().isNotEmpty) return '@${item.userName.trim()}';
    return 'Пользователь';
  }

  int _totalPhotoReactions(List<PhotoReactionSummary> items) {
    return ReactionSummaryUtils.totalCountFromPairs(
      items,
      (item) => item.count,
    );
  }

  int _totalCommentReactions(CommentItem item) {
    return ReactionSummaryUtils.totalCountFromPairs(
      item.reactions,
      (reaction) => reaction.count,
    );
  }

  int _topPhotoReactionType(List<PhotoReactionSummary> items) {
    return ReactionSummaryUtils.topReactionTypeFromPairs(
      items,
      typeSelector: (item) => item.type,
      countSelector: (item) => item.count,
    );
  }

  int _topCommentReactionType(CommentItem item) {
    return ReactionSummaryUtils.topReactionTypeFromPairs(
      item.reactions,
      typeSelector: (reaction) => reaction.type,
      countSelector: (reaction) => reaction.count,
    );
  }

  Future<void> _togglePrimaryPhotoReaction() async {
    final current = _photo?.myReaction ?? 0;

    if (current == 0) {
      await _onReactionTap(ReactionCatalog.primary);
      return;
    }

    if (_updatingReaction) return;

    setState(() {
      _updatingReaction = true;
    });

    try {
      await _reactionsApi.removeReaction(photoId: widget.photoId);
      if (!mounted) return;
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;
      _showSnack('Не удалось обновить реакцию: ${_normalizeError(e)}');
    } finally {
      if (mounted) {
        setState(() {
          _updatingReaction = false;
        });
      }
    }
  }

  Future<void> _togglePrimaryCommentReaction(CommentItem item) async {
    if (_updatingCommentReactionIds.contains(item.id)) return;

    setState(() {
      _updatingCommentReactionIds.add(item.id);
    });

    try {
      if (item.myReaction == 0) {
        await _commentsApi.setCommentReaction(
          commentId: item.id,
          type: ReactionCatalog.primary.type,
        );
      } else {
        await _commentsApi.removeCommentReaction(commentId: item.id);
      }

      if (!mounted) return;
      await _load(silent: true);
    } catch (e) {
      if (!mounted) return;
      _showSnack(
        'Не удалось обновить реакцию комментария: ${_normalizeError(e)}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _updatingCommentReactionIds.remove(item.id);
        });
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_loading) {
      return const Scaffold(
        backgroundColor: AppColors.background,
        body: Center(
          child: CircularProgressIndicator(),
        ),
      );
    }

      if (_error != null || _photo == null) {
      final state = _buildUnavailableState();

      return Scaffold(
        backgroundColor: AppColors.background,
        body: SafeArea(
          child: InMomentResponsiveContent(
            child: RefreshIndicator(
            onRefresh: () => _load(force: true),
            child: ListView(
              physics: const AlwaysScrollableScrollPhysics(
                parent: BouncingScrollPhysics(),
              ),
              padding: const EdgeInsets.fromLTRB(16, 12, 16, 24),
              children: [
                const _DetailsTopBar(),
                SizedBox(
                  height: MediaQuery.sizeOf(context).height * 0.18,
                ),
                Icon(
                  state.icon,
                  color: AppColors.textSecondary,
                  size: 54,
                ),
                const SizedBox(height: 16),
                Text(
                  state.title,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: AppColors.textPrimary,
                    fontSize: 18,
                    fontWeight: FontWeight.w800,
                    height: 1.25,
                  ),
                ),
                const SizedBox(height: 10),
                Text(
                  state.message,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 14,
                    height: 1.45,
                  ),
                ),
                const SizedBox(height: 18),
                Center(
                  child: FilledButton.icon(
                    onPressed: () => _load(force: true),
                    icon: const Icon(Icons.refresh_rounded),
                    label: const Text('Обновить'),
                  ),
                ),
                const SizedBox(height: 10),
                const Text(
                  'Можно также потянуть экран вниз, чтобы повторить загрузку.',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                    height: 1.35,
                  ),
                ),
              ],
              ),
            ),
          ),
        ),
      );
    }

    final photo = _photo!;

    return Scaffold(
      resizeToAvoidBottomInset: true,
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: InMomentResponsiveContent(
          child: Column(
          children: [
            const Padding(
              padding: EdgeInsets.fromLTRB(16, 12, 16, 0),
              child: _DetailsTopBar(),
            ),
            Expanded(
              child: RefreshIndicator(
                onRefresh: () => _load(force: true),
                child: ListView(
                  controller: _scrollController,
                  physics: const AlwaysScrollableScrollPhysics(),
                  padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
                  children: [
                   _PublicationCard(
                      photoUrl: photo.url,
                      contentType: photo.contentType,
                      caption: photo.caption,
                      authorName: _displayName(photo),
                      userName: photo.authorUserName,
                      authorPhotoUrl: photo.authorProfilePhotoUrl,
                      authorIsActive: photo.authorIsActive,
                      createdAt: _formatDate(photo.createdAt),
                      commentsCount: _comments.length,
                      reactionsCount: _totalPhotoReactions(photo.reactions),
                      reactionTypes: photo.reactions.map((r) => r.type).toList(),
                      topReactionType: _topPhotoReactionType(photo.reactions),
                      hasMyReaction: photo.myReaction != 0,
                      updatingReaction: _updatingReaction,
                      onReactionTap: _togglePrimaryPhotoReaction,
                      onReactionPickerOpen: _showPhotoReactionPickerAt,
                      onCommentTap: _focusComposer,
                      onMenuTap: _showPhotoMenu,
                      onMentionTap: _openMentionByUserName,
                      onAuthorTap: () {
                        if (!photo.authorIsActive) {
                          _showSnack('Профиль автора недоступен.');
                          return;
                        }

                        Navigator.of(context).push(
                          MaterialPageRoute(
                            builder: (_) => PublicUserProfilePage(
                              userId: photo.authorId,
                            ),
                          ),
                        );
                      },
                    ),
                    const SizedBox(height: 18),
                    _SectionHeader(
                      title: 'Обсуждение',
                      subtitle: _comments.isEmpty
                          ? 'Пока без комментариев'
                          : '${_comments.length} комментариев',
                    ),
                    const SizedBox(height: 12),
                    if (_comments.isEmpty)
                      const _EmptyCommentsState()
                    else
                    ..._comments.map(
                        (item) => Padding(
                          key: _commentKeys[item.id],
                          padding: const EdgeInsets.only(bottom: 10),
                          child: _CommentCard(
                            item: item,
                            displayName: _commentDisplayName(item),
                            createdAt: _formatDate(item.createdAt),
                            replyPreview: item.parentCommentTextPreview,
                            replyUserName: item.parentCommentUserName,
                            authorIsActive: item.userIsActive,
                            parentAuthorIsActive: item.parentCommentUserIsActive,
                            reactionsUpdating: _updatingCommentReactionIds.contains(item.id),
                            reactionsCount: _totalCommentReactions(item),
                            topReactionType: _topCommentReactionType(item),
                            hasMyReaction: item.myReaction != 0,
                            highlighted: _highlightedCommentId == item.id,
                            onQuickReact: () => _togglePrimaryCommentReaction(item),
                            onReply: () => _startReply(item),
                            onEdit: item.isMine && !_editingCommentIds.contains(item.id)
                                ? () => _editComment(item)
                                : null,
                            onDelete: item.isMine && !_deletingCommentIds.contains(item.id)
                                ? () => _deleteComment(item)
                                : null,
                            onReport: item.isMine || !item.userIsActive
                                ? null
                                : () => _reportComment(item),
                            onReportAuthor: item.isMine || !item.userIsActive
                                ? null
                                : () => _reportCommentAuthor(item),
                            onBlock: item.isMine || !item.userIsActive
                                ? null
                                : () => _blockCommentAuthor(item),
                            onMentionTap: _openMentionByUserName,
                            onAuthorTap: () {
                              Navigator.of(context).push(
                                MaterialPageRoute(
                                  builder: (_) => PublicUserProfilePage(
                                    userId: item.userId,
                                  ),
                                ),
                              );
                            },
                          ),
                        ),
                      ),
                  ],
                ),
              ),
            ),
            SafeArea(
              top: false,
              child: _CommentComposer(
                controller: _commentController,
                focusNode: _commentFocusNode,
                submitting: _submittingComment,
                replyTo: _replyTo,
                selectedGifUrl: _selectedGifUrl,
                onCancelReply: _cancelReply,
                onSend: _submitComment,
                onInsertEmoji: _insertIntoComment,
                onGifTap: _showGifPickerSheet,
                onClearGif: () {
                  setState(() {
                    _selectedGifUrl = null;
                  });
                },
                groupId: widget.groupId ?? _photo?.groupId,
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

    _UnavailablePhotoState _buildUnavailableState() {
    switch (_failureType) {
      case PhotoDetailsFailureType.forbidden:
        return const _UnavailablePhotoState(
          icon: Icons.lock_outline_rounded,
          title: 'Публикация недоступна',
          message:
              'Скорее всего, у вас больше нет доступа к этой публикации или пользователь сейчас заблокирован.',
        );
      case PhotoDetailsFailureType.notFound:
        return const _UnavailablePhotoState(
          icon: Icons.delete_outline_rounded,
          title: 'Публикация не найдена',
          message:
              'Эта публикация была удалена или больше не существует.',
        );
      case PhotoDetailsFailureType.unauthorized:
        return const _UnavailablePhotoState(
          icon: Icons.login_rounded,
          title: 'Нужен повторный вход',
          message:
              'Сессия истекла. Авторизуйтесь снова, чтобы открыть публикацию.',
        );
      case PhotoDetailsFailureType.methodNotAllowed:
        return const _UnavailablePhotoState(
          icon: Icons.error_outline_rounded,
          title: 'Публикация временно недоступна',
          message:
              'Попробуйте открыть её позже.',
        );
      case PhotoDetailsFailureType.unknown:
      case null:
        return _UnavailablePhotoState(
          icon: Icons.broken_image_outlined,
          title: 'Не удалось открыть публикацию',
          message: _error ?? 'Неизвестная ошибка',
        );
    }
  }
}

class _DetailsTopBar extends StatelessWidget {
  const _DetailsTopBar();

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        _TopCircleButton(
          icon: Icons.arrow_back_ios_new_rounded,
          onTap: () => Navigator.of(context).pop(),
        ),
        const Spacer(),
        const Text(
          'Публикация',
          style: TextStyle(
            color: AppColors.textPrimary,
            fontSize: 17,
            fontWeight: FontWeight.w800,
          ),
        ),
        const Spacer(),
        const SizedBox(
          width: 44,
          height: 44,
        ),
      ],
    );
  }
}

class _PublicationCard extends StatelessWidget {
  final String photoUrl;
  final String contentType;
  final String? caption;
  final String authorName;
  final String userName;
  final String? authorPhotoUrl;
  final bool authorIsActive;
  final String createdAt;
  final int commentsCount;
  final int reactionsCount;
  final bool hasMyReaction;
  final bool updatingReaction;
  final VoidCallback onReactionTap;
  final VoidCallback onCommentTap;
  final VoidCallback onMenuTap;
  final ValueChanged<String> onMentionTap;
  final VoidCallback onAuthorTap;
  final int topReactionType;
  final List<int> reactionTypes;
  final ValueChanged<Offset> onReactionPickerOpen;

  const _PublicationCard({
    required this.photoUrl,
    required this.contentType,
    required this.caption,
    required this.authorName,
    required this.userName,
    required this.authorPhotoUrl,
    required this.authorIsActive,
    required this.createdAt,
    required this.commentsCount,
    required this.reactionsCount,
    required this.hasMyReaction,
    required this.updatingReaction,
    required this.onReactionTap,
    required this.onCommentTap,
    required this.onMenuTap,
    required this.onMentionTap,
    required this.onAuthorTap,
    required this.topReactionType,
    required this.reactionTypes,
    required this.onReactionPickerOpen,
  });

  @override
  Widget build(BuildContext context) {
    final trimmedCaption = (caption ?? '').trim();

    return Container(
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(30),
        border: Border.all(color: AppColors.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClipRRect(
            borderRadius: const BorderRadius.vertical(
              top: Radius.circular(30),
            ),
            child: _AdaptivePublicationMedia(
              url: photoUrl,
              contentType: contentType,
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(16, 14, 16, 16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    GestureDetector(
                      onTap: onAuthorTap,
                      child: CircleAvatar(
                        radius: 22,
                        backgroundColor: AppColors.accent.withValues(alpha: 0.22),
                        backgroundImage: authorIsActive &&
                                authorPhotoUrl != null &&
                                authorPhotoUrl!.trim().isNotEmpty
                            ? NetworkImage(authorPhotoUrl!)
                            : null,
                        child: !authorIsActive
                            ? const Icon(
                                Icons.person_off_rounded,
                                color: AppColors.textPrimary,
                              )
                            : (authorPhotoUrl == null ||
                                    authorPhotoUrl!.trim().isEmpty)
                                ? Text(
                                    userName.isNotEmpty
                                        ? userName[0].toUpperCase()
                                        : 'U',
                                    style: const TextStyle(
                                      color: AppColors.textPrimary,
                                      fontWeight: FontWeight.w700,
                                    ),
                                  )
                                : null,
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: GestureDetector(
                        onTap: onAuthorTap,
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              authorName,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(
                                color: AppColors.textPrimary,
                                fontSize: 16,
                                fontWeight: FontWeight.w800,
                              ),
                            ),
                            if (authorIsActive && userName.trim().isNotEmpty) ...[
                              const SizedBox(height: 3),
                              Text(
                                '@$userName',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(
                                  color: AppColors.textSecondary,
                                  fontSize: 12,
                                  fontWeight: FontWeight.w600,
                                ),
                              ),
                            ],
                          ],
                        ),
                      ),
                    ),
                    const SizedBox(width: 8),
                    _InlineMenuButton(
                      onTap: onMenuTap,
                    ),
                  ],
                ),
                if (trimmedCaption.isNotEmpty) ...[
                  const SizedBox(height: 14),
                  MentionText(
                    text: trimmedCaption,
                    onMentionTap: onMentionTap,
                    style: const TextStyle(
                      color: AppColors.textPrimary,
                      fontSize: 14,
                      height: 1.48,
                    ),
                    mentionStyle: const TextStyle(
                      color: AppColors.accentSecondary,
                      fontSize: 14,
                      height: 1.48,
                      fontWeight: FontWeight.w800,
                    ),
                  ),
                ],
                const SizedBox(height: 14),
                Row(
                  children: [
                    Expanded(
                      child: Text(
                        createdAt,
                        style: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 12,
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),
                    ReactionCounterPill(
                      value: '$reactionsCount',
                      topReactionType: topReactionType,
                      reactionTypes: reactionTypes,
                      selected: hasMyReaction,
                      loading: updatingReaction,
                      onTap: onReactionTap,
                      onOpenPicker: onReactionPickerOpen,
                    ),
                    const SizedBox(width: 8),
                    Material(
                      color: AppColors.surface,
                      borderRadius: BorderRadius.circular(999),
                      child: InkWell(
                        onTap: onCommentTap,
                        borderRadius: BorderRadius.circular(999),
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
                          decoration: BoxDecoration(
                            borderRadius: BorderRadius.circular(999),
                            border: Border.all(color: AppColors.border),
                          ),
                          child: Row(
                            mainAxisSize: MainAxisSize.min,
                            children: [
                              const Icon(
                                Icons.mode_comment_outlined,
                                size: 15,
                                color: AppColors.textSecondary,
                              ),
                              const SizedBox(width: 5),
                              Text(
                                '$commentsCount',
                                style: const TextStyle(
                                  color: AppColors.textPrimary,
                                  fontSize: 12,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _AdaptivePublicationMedia extends StatefulWidget {
  final String url;
  final String contentType;

  const _AdaptivePublicationMedia({
    required this.url,
    required this.contentType,
  });

  @override
  State<_AdaptivePublicationMedia> createState() =>
      _AdaptivePublicationMediaState();
}

class _AdaptivePublicationMediaState extends State<_AdaptivePublicationMedia> {
  double? _aspectRatio;
  ImageStream? _imageStream;
  ImageStreamListener? _imageListener;
  int _generation = 0;

  bool get _isVideo => widget.contentType.toLowerCase().startsWith('video/');
  bool get _isImage => widget.contentType.toLowerCase().startsWith('image/');

  @override
  void initState() {
    super.initState();
    _resolveAspectRatio();
  }

  @override
  void didUpdateWidget(covariant _AdaptivePublicationMedia oldWidget) {
    super.didUpdateWidget(oldWidget);

    if (oldWidget.url != widget.url ||
        oldWidget.contentType != widget.contentType) {
      _resolveAspectRatio();
    }
  }

  @override
  void dispose() {
    _generation++;
    _detachImageListener();
    super.dispose();
  }

  void _detachImageListener() {
    final stream = _imageStream;
    final listener = _imageListener;

    if (stream != null && listener != null) {
      stream.removeListener(listener);
    }

    _imageStream = null;
    _imageListener = null;
  }

  void _resolveAspectRatio() {
    _generation++;
    _detachImageListener();

    final url = widget.url.trim();

    setState(() {
      _aspectRatio = null;
    });

    if (url.isEmpty) return;

    if (_isImage) {
      _resolveImageAspectRatio(url);
      return;
    }

    if (_isVideo) {
      setState(() {
        _aspectRatio = 9 / 16;
      });
    }
  }

  void _resolveImageAspectRatio(String url) {
    final generation = _generation;
    final provider = NetworkImage(url);
    final stream = provider.resolve(const ImageConfiguration());

    late final ImageStreamListener listener;

    listener = ImageStreamListener(
      (info, _) {
        final width = info.image.width;
        final height = info.image.height;

        if (!mounted || generation != _generation) return;
        if (width <= 0 || height <= 0) return;

        setState(() {
          _aspectRatio = width / height;
        });
      },
      onError: (_, _) {
        if (!mounted || generation != _generation) return;

        setState(() {
          _aspectRatio = 4 / 5;
        });
      },
    );

    _imageStream = stream;
    _imageListener = listener;
    stream.addListener(listener);
  }

  double _heightForWidth({
    required double width,
    required Size screen,
  }) {
    final ratio = (_aspectRatio ?? 4 / 5).clamp(0.48, 2.2);
    final rawHeight = width / ratio;

    final minHeight = screen.height < 720 ? 240.0 : 300.0;
    final maxHeight = screen.height * 0.72;

    return rawHeight.clamp(minHeight, maxHeight);
  }

  @override
  Widget build(BuildContext context) {
    final url = widget.url.trim();

    return LayoutBuilder(
      builder: (context, constraints) {
        final screen = MediaQuery.sizeOf(context);
        final width = constraints.maxWidth.isFinite
            ? constraints.maxWidth
            : screen.width;

        final height = _heightForWidth(
          width: width,
          screen: screen,
        );

        return AnimatedContainer(
          duration: const Duration(milliseconds: 180),
          curve: Curves.easeOut,
          width: double.infinity,
          height: height,
          color: AppColors.background.withValues(alpha: 0.72),
          alignment: Alignment.center,
          child: NetworkVisualMedia(
            url: url,
            contentType: widget.contentType,
            allowInlineVideo: true,
            autoplay: false,
            looping: false,
            startMuted: true,
            showControls: isVideoContentType(widget.contentType),
            allowPlaybackSpeedChanging: true,
            showVideoBadge: true,
            fit: BoxFit.contain,
            placeholderLabel: 'Не удалось загрузить медиа',
          ),
        );
      },
    );
  }
}

class _InlineMenuButton extends StatelessWidget {
  final VoidCallback onTap;

  const _InlineMenuButton({
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(999),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: Container(
          width: 36,
          height: 36,
          alignment: Alignment.center,
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: AppColors.border),
          ),
          child: const Icon(
            Icons.more_horiz_rounded,
            size: 18,
            color: AppColors.textSecondary,
          ),
        ),
      ),
    );
  }
}

class _SectionHeader extends StatelessWidget {
  final String title;
  final String subtitle;

  const _SectionHeader({
    required this.title,
    required this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        Expanded(
          child: Text(
            title,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 20,
              fontWeight: FontWeight.w800,
            ),
          ),
        ),
        const SizedBox(width: 12),
        Text(
          subtitle,
          style: const TextStyle(
            color: AppColors.textSecondary,
            fontSize: 13,
          ),
        ),
      ],
    );
  }
}

class _CommentCard extends StatelessWidget {
  final CommentItem item;
  final String displayName;
  final String createdAt;
  final String? replyPreview;
  final String? replyUserName;
  final bool authorIsActive;
  final bool? parentAuthorIsActive;
  final bool reactionsUpdating;
  final int reactionsCount;
  final bool hasMyReaction;
  final bool highlighted;
  final VoidCallback onQuickReact;
  final VoidCallback onReply;
  final VoidCallback onAuthorTap;
  final VoidCallback? onEdit;
  final VoidCallback? onDelete;
  final VoidCallback? onReport;
  final VoidCallback? onReportAuthor;
  final VoidCallback? onBlock;
  final ValueChanged<String> onMentionTap;
  final int topReactionType;

  const _CommentCard({
    required this.item,
    required this.displayName,
    required this.createdAt,
    required this.replyPreview,
    required this.replyUserName,
    required this.authorIsActive,
    required this.parentAuthorIsActive,
    required this.reactionsUpdating,
    required this.reactionsCount,
    required this.hasMyReaction,
    required this.highlighted,
    required this.onQuickReact,
    required this.onReply,
    required this.onAuthorTap,
    required this.onEdit,
    required this.onDelete,
    required this.onReport,
    required this.onReportAuthor,
    required this.onBlock,
    required this.onMentionTap,
    required this.topReactionType,
  });

  @override
  Widget build(BuildContext context) {
    final replyText = (replyPreview ?? '').trim();

    final safeReplyLabel = parentAuthorIsActive == false
        ? 'Деактивированный пользователь'
        : ((replyUserName ?? '').trim().isNotEmpty
            ? '@${replyUserName!.trim()}'
            : 'Комментарий');

    return AnimatedContainer(
      duration: const Duration(milliseconds: 220),
      curve: Curves.easeOut,
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: highlighted
            ? AppColors.accent.withValues(alpha: 0.14)
            : AppColors.card,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(
          color: highlighted
              ? AppColors.accentSecondary
              : AppColors.border,
          width: highlighted ? 1.4 : 1,
        ),
        boxShadow: highlighted
            ? [
                BoxShadow(
                  color: AppColors.accent.withValues(alpha: 0.18),
                  blurRadius: 18,
                  offset: const Offset(0, 8),
                ),
              ]
            : null,
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              GestureDetector(
                onTap: onAuthorTap,
                child: CircleAvatar(
                  radius: 18,
                  backgroundColor: AppColors.accent.withValues(alpha: 0.22),
                  backgroundImage: authorIsActive &&
                          item.profilePhotoUrl != null &&
                          item.profilePhotoUrl!.trim().isNotEmpty
                      ? NetworkImage(item.profilePhotoUrl!)
                      : null,
                  child: !authorIsActive
                      ? const Icon(
                          Icons.person_off_rounded,
                          size: 18,
                          color: AppColors.textPrimary,
                        )
                      : (item.profilePhotoUrl == null ||
                              item.profilePhotoUrl!.trim().isEmpty)
                          ? Text(
                              item.userName.isNotEmpty
                                  ? item.userName[0].toUpperCase()
                                  : 'U',
                              style: const TextStyle(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.w700,
                              ),
                            )
                          : null,
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: GestureDetector(
                  onTap: onAuthorTap,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        displayName,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          color: AppColors.textPrimary,
                          fontSize: 14,
                          fontWeight: FontWeight.w800,
                        ),
                      ),
                      if (authorIsActive && item.userName.trim().isNotEmpty) ...[
                        const SizedBox(height: 2),
                        Text(
                          '@${item.userName}',
                          maxLines: 1,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
              ),
              const SizedBox(width: 8),
              PopupMenuButton<String>(
                color: AppColors.surfaceElevated.withValues(alpha: 0.94),
                elevation: 10,
                shadowColor: AppColors.black.withValues(alpha: 0.22),
                surfaceTintColor: Colors.transparent,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(22),
                  side: BorderSide(
                    color: AppColors.purpleStroke(0.26),
                  ),
                ),
                iconColor: AppColors.textSecondary,
                onSelected: (value) {
                  if (value == 'reply') onReply();
                  if (value == 'edit') onEdit?.call();
                  if (value == 'delete') onDelete?.call();
                  if (value == 'report') onReport?.call();
                  if (value == 'report_author') onReportAuthor?.call();
                  if (value == 'block') onBlock?.call();
                },
                itemBuilder: (context) => [
                  const PopupMenuItem<String>(
                    value: 'reply',
                    child: Text('Ответить'),
                  ),
                  if (onEdit != null)
                    const PopupMenuItem<String>(
                      value: 'edit',
                      child: Text('Редактировать'),
                    ),
                  if (onDelete != null)
                    const PopupMenuItem<String>(
                      value: 'delete',
                      child: Text('Удалить'),
                    ),
                  if (onReport != null)
                    const PopupMenuItem<String>(
                      value: 'report',
                      child: Text('Пожаловаться'),
                    ),
                  if (onReportAuthor != null)
                    const PopupMenuItem<String>(
                      value: 'report_author',
                      child: Text('Пожаловаться на автора'),
                    ),
                  if (onBlock != null)
                    const PopupMenuItem<String>(
                      value: 'block',
                      child: Text('Заблокировать автора'),
                    ),
                ],
              ),
            ],
          ),
          if (replyText.isNotEmpty) ...[
            const SizedBox(height: 12),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              decoration: BoxDecoration(
                color: AppColors.surface.withValues(alpha: 0.86),
                borderRadius: BorderRadius.circular(16),
                border: Border.all(
                  color: AppColors.border.withValues(alpha: 0.92),
                ),
              ),
              child: Text(
                '$safeReplyLabel: $replyText',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: AppColors.textSecondary,
                  fontSize: 12,
                  height: 1.35,
                ),
              ),
            ),
          ],
          const SizedBox(height: 12),
          if (item.text.trim().isNotEmpty)
            MentionText(
              text: item.text,
              onMentionTap: onMentionTap,
              style: const TextStyle(
                color: AppColors.textPrimary,
                fontSize: 14,
                height: 1.45,
              ),
              mentionStyle: const TextStyle(
                color: AppColors.accentSecondary,
                fontSize: 14,
                height: 1.45,
                fontWeight: FontWeight.w800,
              ),
            ),
          if ((item.gifUrl ?? '').trim().isNotEmpty) ...[
            SizedBox(height: item.text.trim().isEmpty ? 0 : 12),
            _CommentGifPreview(
              gifUrl: item.gifUrl!.trim(),
            ),
          ],
          if (item.editedAt != null) ...[
            const SizedBox(height: 8),
            const Text(
              'изменено',
              style: TextStyle(
                color: AppColors.textSecondary,
                fontSize: 11,
              ),
            ),
          ],
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: Text(
                  createdAt,
                  style: const TextStyle(
                    color: AppColors.textSecondary,
                    fontSize: 12,
                  ),
                ),
              ),
              const SizedBox(width: 10),
              ReactionCounterPill(
                value: '$reactionsCount',
                topReactionType: 1,
                reactionTypes: const [1],
                heartOnly: true,
                selected: hasMyReaction,
                loading: reactionsUpdating,
                onTap: onQuickReact,
                onOpenPicker: null,
              ),
              const SizedBox(width: 8),
              _ActionTextButton(
                label: 'Ответить',
                onTap: onReply,
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _CommentGifPreview extends StatelessWidget {
  final String gifUrl;

  const _CommentGifPreview({
    required this.gifUrl,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      constraints: const BoxConstraints(
        maxHeight: 260,
      ),
      width: double.infinity,
      decoration: BoxDecoration(
        color: AppColors.surface,
        borderRadius: BorderRadius.circular(18),
      ),
      clipBehavior: Clip.antiAlias,
      child: Image.network(
        gifUrl,
        fit: BoxFit.cover,
        filterQuality: FilterQuality.low,
        loadingBuilder: (context, child, loadingProgress) {
          if (loadingProgress == null) return child;

          return const SizedBox(
            height: 180,
            child: Center(
              child: SizedBox(
                width: 18,
                height: 18,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ),
          );
        },
        errorBuilder: (_, _, _) {
          return const SizedBox(
            height: 180,
            child: Center(
              child: Icon(
                Icons.gif_box_outlined,
                color: AppColors.textSecondary,
              ),
            ),
          );
        },
      ),
    );
  }
}

class _ActionTextButton extends StatelessWidget {
  final String label;
  final VoidCallback onTap;

  const _ActionTextButton({
    required this.label,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface,
      borderRadius: BorderRadius.circular(999),
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(999),
        child: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(999),
            border: Border.all(color: AppColors.border),
          ),
          child: Text(
            label,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 12,
              fontWeight: FontWeight.w700,
            ),
          ),
        ),
      ),
    );
  }
}

class _CommentComposer extends StatefulWidget {
  final TextEditingController controller;
  final FocusNode focusNode;
  final bool submitting;
  final CommentItem? replyTo;
  final VoidCallback onCancelReply;
  final VoidCallback onSend;
  final ValueChanged<String> onInsertEmoji;
  final VoidCallback onGifTap;
  final String? groupId;
  final String? selectedGifUrl;
  final VoidCallback onClearGif;

  const _CommentComposer({
    required this.controller,
    required this.focusNode,
    required this.submitting,
    required this.replyTo,
    required this.onCancelReply,
    required this.onSend,
    required this.onInsertEmoji,
    required this.onGifTap,
    required this.groupId,
    required this.selectedGifUrl,
    required this.onClearGif,
  });

  @override
  State<_CommentComposer> createState() => _CommentComposerState();
}

class _CommentComposerState extends State<_CommentComposer> {
  bool _emojiOpen = false;
  bool _hasText = false;

 static const _emojis = [
  '❤️','💜','🖤','💔','💕','💞','🥰','😍','😘','🥹','😭',
  '😂','🤣','🤯','😮','😳','😤','😡','😱','😴','😎','🤭','🙈',
  '🔥','✨','🎉','👏','🫶','🙌',
  '👍','👎','🙏','💪','🤝','🤌',
  '🐱','🐶','🌸','🍿','🍌','🍑',
  '😈','👿','👺','💀','🤡',
 ];

  void _toggleEmojiPanel() {
    if (_emojiOpen) {
      setState(() {
        _emojiOpen = false;
      });
      widget.focusNode.requestFocus();
      return;
    }

    widget.focusNode.unfocus();

    Future<void>.delayed(const Duration(milliseconds: 120), () {
      if (!mounted) return;
      setState(() {
        _emojiOpen = true;
      });
    });
  }

  void _send() {
    final hasGif = (widget.selectedGifUrl ?? '').trim().isNotEmpty;

    if ((!_hasText && !hasGif) || widget.submitting) return;

    setState(() {
      _emojiOpen = false;
    });

    widget.onSend();
  }

  @override
  Widget build(BuildContext context) {
    final media = MediaQuery.of(context);
    final rawInset = media.viewInsets.bottom;

    final bottomInset = _emojiOpen
        ? 0.0
        : rawInset.clamp(0.0, media.size.height * 0.34);
    final hasGif = (widget.selectedGifUrl ?? '').trim().isNotEmpty;
    final canSend = (_hasText || hasGif) && !widget.submitting;

    return AnimatedContainer(
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
      padding: EdgeInsets.only(bottom: bottomInset > 0 ? 8 : 0),
      decoration: BoxDecoration(
        color: AppColors.background,
        border: Border(
          top: BorderSide(
            color: AppColors.border.withValues(alpha: 0.55),
          ),
        ),
      ),
      child: SafeArea(
        top: false,
        child: Padding(
          padding: const EdgeInsets.fromLTRB(12, 10, 12, 10),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.replyTo != null)
                Container(
                  width: double.infinity,
                  margin: const EdgeInsets.only(bottom: 10),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 12,
                    vertical: 10,
                  ),
                  decoration: BoxDecoration(
                    color: AppColors.card,
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Row(
                    children: [
                      Container(
                        width: 3,
                        height: 34,
                        decoration: BoxDecoration(
                          color: AppColors.accentSecondary,
                          borderRadius: BorderRadius.circular(999),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          'Ответ для @${widget.replyTo!.userName}: ${widget.replyTo!.text.trim()}',
                          maxLines: 2,
                          overflow: TextOverflow.ellipsis,
                          style: const TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 12,
                            height: 1.35,
                          ),
                        ),
                      ),
                      const SizedBox(width: 10),
                      InkWell(
                        onTap: widget.onCancelReply,
                        borderRadius: BorderRadius.circular(999),
                        child: const Padding(
                          padding: EdgeInsets.all(4),
                          child: Icon(
                            Icons.close_rounded,
                            size: 18,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              Row(
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  _ComposerCircleButton(
                    icon: _emojiOpen
                        ? Icons.keyboard_rounded
                        : Icons.emoji_emotions_outlined,
                    onTap: widget.submitting ? null : _toggleEmojiPanel,
                  ),
                  const SizedBox(width: 8),
                  _ComposerCircleButton(
                    icon: Icons.gif_box_outlined,
                    onTap: widget.submitting ? null : widget.onGifTap,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: MentionTextField(
                      controller: widget.controller,
                      focusNode: widget.focusNode,
                      enabled: !widget.submitting,
                      minLines: 1,
                      maxLines: 3,
                      groupId: widget.groupId,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 16,
                        height: 1.2,
                      ),
                      decoration: InputDecoration(
                        hintText: widget.replyTo == null ? 'Комментарий' : 'Ответ',
                        hintStyle: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 16,
                          height: 1.2,
                        ),
                        filled: true,
                        fillColor: AppColors.card,
                        isDense: true,
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: 16,
                          vertical: 12,
                        ),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(24),
                          borderSide: BorderSide.none,
                        ),
                        enabledBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(24),
                          borderSide: BorderSide(
                            color: AppColors.border.withValues(alpha: 0.9),
                            width: 1,
                          ),
                        ),
                        focusedBorder: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(24),
                          borderSide: const BorderSide(
                            color: AppColors.accentSecondary,
                            width: 1.2,
                          ),
                        ),
                        counterText: '',
                      ),
                    ),
                  ),
                  const SizedBox(width: 10),
                  AnimatedOpacity(
                    duration: const Duration(milliseconds: 140),
                    opacity: canSend ? 1 : 0.45,
                    child: Material(
                      color: canSend ? AppColors.accentSecondary : AppColors.surface,
                      shape: const CircleBorder(),
                      child: InkWell(
                        onTap: canSend ? _send : null,
                        customBorder: const CircleBorder(),
                        child: SizedBox(
                          width: 44,
                          height: 44,
                          child: widget.submitting
                              ? const Padding(
                                  padding: EdgeInsets.all(14),
                                  child: CircularProgressIndicator(strokeWidth: 2),
                                )
                              : const Icon(
                                  Icons.send_rounded,
                                  color: AppColors.textPrimary,
                                ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
              if ((widget.selectedGifUrl ?? '').trim().isNotEmpty)
                Container(
                  width: double.infinity,
                  margin: const EdgeInsets.only(top: 10),
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: AppColors.card,
                    borderRadius: BorderRadius.circular(20),
                    border: Border.all(color: AppColors.border),
                  ),
                  child: Stack(
                    children: [
                      ClipRRect(
                        borderRadius: BorderRadius.circular(16),
                        child: SizedBox(
                          height: 120,
                          width: double.infinity,
                          child: Image.network(
                            widget.selectedGifUrl!.trim(),
                            fit: BoxFit.cover,
                            filterQuality: FilterQuality.low,
                            frameBuilder: (context, child, frame, wasSynchronouslyLoaded) {
                              if (frame == null) {
                                return const Center(
                                  child: SizedBox(
                                    width: 18,
                                    height: 18,
                                    child: CircularProgressIndicator(strokeWidth: 2),
                                  ),
                                );
                              }
                              return child;
                            },
                            errorBuilder: (_, _, _) {
                              return const Center(
                                child: Icon(
                                  Icons.gif_box_outlined,
                                  color: AppColors.textSecondary,
                                ),
                              );
                            },
                          )
                        ),
                      ),
                      Positioned(
                        top: 8,
                        right: 8,
                        child: Material(
                          color: AppColors.background.withValues(alpha: 0.78),
                          shape: const CircleBorder(),
                          child: InkWell(
                            onTap: widget.onClearGif,
                            customBorder: const CircleBorder(),
                            child: const SizedBox(
                              width: 34,
                              height: 34,
                              child: Icon(
                                Icons.close_rounded,
                                color: AppColors.textPrimary,
                                size: 18,
                              ),
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              AnimatedSize(
                duration: const Duration(milliseconds: 160),
                curve: Curves.easeOut,
                alignment: Alignment.topCenter,
                child: !_emojiOpen
                    ? const SizedBox.shrink()
                    : SizedBox(
                        height: 158,
                        child: Container(
                          width: double.infinity,
                          margin: const EdgeInsets.only(top: 10),
                          padding: const EdgeInsets.all(10),
                          decoration: BoxDecoration(
                            color: AppColors.card,
                            borderRadius: BorderRadius.circular(20),
                            border: Border.all(color: AppColors.border),
                          ),
                          child: SingleChildScrollView(
                            keyboardDismissBehavior:
                                ScrollViewKeyboardDismissBehavior.manual,
                            child: Wrap(
                              spacing: 8,
                              runSpacing: 8,
                              children: [
                                for (final emoji in _emojis)
                                  InkWell(
                                    onTap: () => widget.onInsertEmoji(emoji),
                                    borderRadius: BorderRadius.circular(999),
                                    child: Container(
                                      width: 38,
                                      height: 38,
                                      alignment: Alignment.center,
                                      decoration: BoxDecoration(
                                        color: AppColors.surface,
                                        borderRadius: BorderRadius.circular(999),
                                        border: Border.all(
                                          color: AppColors.border.withValues(alpha: 0.7),
                                        ),
                                      ),
                                      child: Text(
                                        emoji,
                                        style: const TextStyle(fontSize: 18),
                                      ),
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ),
                      ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  void initState() {
    super.initState();
    _hasText = widget.controller.text.trim().isNotEmpty;
    widget.controller.addListener(_handleTextChanged);
    widget.focusNode.addListener(_handleFocusChanged);
  }

  @override
  void dispose() {
    widget.controller.removeListener(_handleTextChanged);
    super.dispose();
    widget.focusNode.removeListener(_handleFocusChanged);
  }

  void _handleTextChanged() {
    final next = widget.controller.text.trim().isNotEmpty;
    if (next == _hasText) return;

    setState(() {
      _hasText = next;
    });
  }

    void _handleFocusChanged() {
    if (!widget.focusNode.hasFocus) return;
    if (!_emojiOpen) return;

    setState(() {
      _emojiOpen = false;
    });
  }
}

class _ComposerCircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _ComposerCircleButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.card,
      shape: const CircleBorder(),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: SizedBox(
          width: 44,
          height: 44,
          child: Icon(
            icon,
            size: 21,
            color: onTap == null
                ? AppColors.textSecondary.withValues(alpha: 0.45)
                : AppColors.textSecondary,
          ),
        ),
      ),
    );
  }
}

class _EmptyCommentsState extends StatelessWidget {
  const _EmptyCommentsState();

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(18),
      decoration: BoxDecoration(
        color: AppColors.card,
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.border),
      ),
      child: const Text(
        'Пока никто не начал обсуждение. Первый комментарий появится прямо здесь под фото.',
        style: TextStyle(
          color: AppColors.textSecondary,
          fontSize: 14,
          height: 1.45,
        ),
      ),
    );
  }
}

class _TopCircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback? onTap;

  const _TopCircleButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.card,
      shape: const CircleBorder(),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: SizedBox(
          width: 44,
          height: 44,
          child: Icon(
            icon,
            color: AppColors.textPrimary,
            size: 20,
          ),
        ),
      ),
    );
  }
}

class _UnavailablePhotoState {
  final IconData icon;
  final String title;
  final String message;

  const _UnavailablePhotoState({
    required this.icon,
    required this.title,
    required this.message,
  });
}