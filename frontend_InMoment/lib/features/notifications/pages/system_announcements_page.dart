import 'package:file_picker/file_picker.dart';
import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';

import '../../../core/api/api_error.dart';
import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/network_visual_media.dart';
import '../api/system_announcements_api.dart';
import '../models/system_announcement.dart';

class SystemAnnouncementsPage extends StatefulWidget {
  const SystemAnnouncementsPage({super.key});

  @override
  State<SystemAnnouncementsPage> createState() =>
      _SystemAnnouncementsPageState();
}

class _SystemAnnouncementsPageState extends State<SystemAnnouncementsPage> {
  final _api = SystemAnnouncementsApi();
  final _textController = TextEditingController();

  bool _loading = true;
  bool _saving = false;
  String? _error;
  List<SystemAnnouncement> _items = const [];

  String? _editingId;
  String? _mediaUrl;
  String? _mediaContentType;

  @override
  void initState() {
    super.initState();
    _load();
  }

  @override
  void dispose() {
    _textController.dispose();
    super.dispose();
  }

  Future<void> _load() async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final items = await _api.list();
      if (!mounted) return;

      setState(() {
        _items = items;
        _loading = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
       _error = ApiError.normalize(
        e,
        fallback: 'Не удалось загрузить уведомления.',
      );
        _loading = false;
      });
    }
  }

  void _resetForm() {
    setState(() {
      _editingId = null;
      _textController.clear();
      _mediaUrl = null;
      _mediaContentType = null;
    });
  }

  void _startEdit(SystemAnnouncement item) {
    if (!item.canEdit) return;

    setState(() {
      _editingId = item.id;
      _textController.text = item.text;
      _mediaUrl = item.mediaUrl;
      _mediaContentType = item.mediaContentType;
    });
  }

  Future<void> _pickMedia() async {
    if (_saving) return;

    final result = await FilePicker.platform.pickFiles(
      type: FileType.media,
      withData: true,
      withReadStream: false,
    );

    if (result == null || result.files.isEmpty) return;

    final file = result.files.single;
    final bytes = file.bytes;
    final path = kIsWeb ? null : file.path;

    if (bytes == null && (path == null || path.trim().isEmpty)) {
      _showMessage('Не удалось получить файл. Попробуйте выбрать его ещё раз.');
      return;
    }
    final contentType = _contentTypeFromFileName(file.name);

    if (contentType == null) {
      _showMessage('Этот формат не поддерживается.');
      return;
    }

    final size = file.size;
    const maxBytes = 200 * 1024 * 1024;
    if (size > maxBytes) {
      _showMessage('Медиа слишком большое. Максимум 200 МБ.');
      return;
    }

    setState(() => _saving = true);

    try {
      final presign = await _api.presignMedia(contentType: contentType);

      await _api.uploadToStorage(
        uploadUrl: presign.uploadUrl,
        contentType: contentType,
        bytes: bytes,
        localFilePath: path,
        contentLength: size,
      );

      if (!mounted) return;

      setState(() {
        _mediaUrl = presign.fileUrl;
        _mediaContentType = contentType;
      });
    } catch (e) {
      if (!mounted) return;
      _showMessage(
        ApiError.normalize(
          e,
          fallback: 'Не удалось загрузить медиа для уведомления.',
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  Future<void> _submit() async {
    final text = _textController.text.trim();

    if (text.isEmpty) {
      _showMessage('Введите текст уведомления.');
      return;
    }

    setState(() => _saving = true);

    try {
      final editingId = _editingId;

      if (editingId == null) {
        await _api.create(
          text: text,
          mediaUrl: _mediaUrl,
          mediaContentType: _mediaContentType,
        );
      } else {
        await _api.update(
          id: editingId,
          text: text,
          mediaUrl: _mediaUrl,
          mediaContentType: _mediaContentType,
        );
      }

      if (!mounted) return;

      _resetForm();
      await _load();
      _showMessage(editingId == null ? 'Уведомление отправлено.' : 'Уведомление обновлено.');
    } catch (e) {
      if (!mounted) return;
      _showMessage(
        ApiError.normalize(
          e,
          fallback: 'Не удалось сохранить уведомление.',
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  String? _contentTypeFromFileName(String name) {
    final lower = name.toLowerCase();

    if (lower.endsWith('.jpg') || lower.endsWith('.jpeg')) return 'image/jpeg';
    if (lower.endsWith('.png')) return 'image/png';
    if (lower.endsWith('.webp')) return 'image/webp';
    if (lower.endsWith('.heic')) return 'image/heic';
    if (lower.endsWith('.heif')) return 'image/heif';

    if (lower.endsWith('.mp4')) return 'video/mp4';
    if (lower.endsWith('.mov')) return 'video/quicktime';
    if (lower.endsWith('.m4v')) return 'video/x-m4v';
    if (lower.endsWith('.webm')) return 'video/webm';
    if (lower.endsWith('.3gp')) return 'video/3gpp';

    return null;
  }

  Future<void> _deleteAnnouncement(SystemAnnouncement item) async {
    if (_saving) return;

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) {
        return AlertDialog(
          backgroundColor: AppColors.surface,
          title: const Text(
            'Удалить объявление?',
            style: TextStyle(color: AppColors.textPrimary),
          ),
          content: const Text(
            'Оно исчезнет из истории и из списка уведомлений пользователей.',
            style: TextStyle(color: AppColors.textSecondary),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.of(context).pop(false),
              child: const Text('Отмена'),
            ),
            TextButton(
              onPressed: () => Navigator.of(context).pop(true),
              child: const Text('Удалить'),
            ),
          ],
        );
      },
    );

    if (confirmed != true) return;

    setState(() => _saving = true);

    try {
      await _api.delete(item.id);

      if (!mounted) return;

      if (_editingId == item.id) {
        _resetForm();
      }

      await _load();
      _showMessage('Объявление удалено.');
    } catch (e) {
      if (!mounted) return;
      _showMessage(
        ApiError.normalize(
          e,
          fallback: 'Не удалось удалить уведомление.',
        ),
      );
    } finally {
      if (mounted) setState(() => _saving = false);
    }
  }

  void _showMessage(String text) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(text)),
    );
  }

  @override
  Widget build(BuildContext context) {
    final editing = _editingId != null;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: RefreshIndicator(
          onRefresh: _load,
          child: ListView(
            padding: const EdgeInsets.fromLTRB(16, 10, 16, 28),
            children: [
              Row(
                children: [
                  _CircleButton(
                    icon: Icons.close_rounded,
                    onTap: () => Navigator.of(context).pop(),
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Text(
                      'Системные уведомления',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 18,
                        fontWeight: FontWeight.w900,
                      ),
                    ),
                  ),
                  const SizedBox(width: 56),
                ],
              ),
              const SizedBox(height: 18),
              _EditorCard(
                textController: _textController,
                editing: editing,
                saving: _saving,
                mediaUrl: _mediaUrl,
                mediaContentType: _mediaContentType,
                onPickMedia: _pickMedia,
                onClearMedia: () {
                  setState(() {
                    _mediaUrl = null;
                    _mediaContentType = null;
                  });
                },
                onCancelEdit: editing ? _resetForm : null,
                onSubmit: _submit,
              ),
              const SizedBox(height: 20),
              const Text(
                'История объявлений',
                style: TextStyle(
                  color: AppColors.textPrimary,
                  fontSize: 15,
                  fontWeight: FontWeight.w900,
                ),
              ),
              const SizedBox(height: 10),
              if (_loading)
                const Center(
                  child: Padding(
                    padding: EdgeInsets.all(32),
                    child: CircularProgressIndicator(),
                  ),
                )
              else if (_error != null)
                _StateCard(
                  icon: Icons.error_outline_rounded,
                  text: _error!,
                )
              else if (_items.isEmpty)
                const _StateCard(
                  icon: Icons.campaign_outlined,
                  text: 'Пока нет отправленных объявлений.',
                )
              else
                ..._items.map(
                  (item) => Padding(
                    padding: const EdgeInsets.only(bottom: 10),
                    child: _AnnouncementHistoryCard(
                      item: item,
                      onEdit: item.canEdit ? () => _startEdit(item) : null,
                      onDelete: () => _deleteAnnouncement(item),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class _EditorCard extends StatelessWidget {
  final TextEditingController textController;
  final bool editing;
  final bool saving;
  final String? mediaUrl;
  final String? mediaContentType;
  final VoidCallback onPickMedia;
  final VoidCallback onClearMedia;
  final VoidCallback? onCancelEdit;
  final VoidCallback onSubmit;

  const _EditorCard({
    required this.textController,
    required this.editing,
    required this.saving,
    required this.mediaUrl,
    required this.mediaContentType,
    required this.onPickMedia,
    required this.onClearMedia,
    required this.onCancelEdit,
    required this.onSubmit,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [
            Colors.white.withValues(alpha: 0.040),
            AppColors.surface.withValues(alpha: 0.36),
            AppColors.backgroundWarm.withValues(alpha: 0.46),
          ],
        ),
        borderRadius: BorderRadius.circular(26),
        border: Border.all(
          color: AppColors.softStroke(0.11),
        ),
      ),
      child: Column(
        children: [
          TextField(
            controller: textController,
            minLines: 4,
            maxLines: 7,
            style: const TextStyle(
              color: AppColors.textPrimary,
              fontSize: 15,
              height: 1.35,
              fontWeight: FontWeight.w700,
            ),
            decoration: InputDecoration(
              hintText: 'Текст системного уведомления',
              hintStyle: TextStyle(
                color: AppColors.textSecondary.withValues(alpha: 0.72),
                fontWeight: FontWeight.w600,
              ),
              filled: true,
              fillColor: AppColors.background.withValues(alpha: 0.34),
              contentPadding: const EdgeInsets.fromLTRB(16, 14, 16, 14),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(20),
                borderSide: BorderSide(
                  color: AppColors.border.withValues(alpha: 0.5),
                ),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(20),
                borderSide: BorderSide(
                  color: AppColors.border.withValues(alpha: 0.5),
                ),
              ),
              focusedBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(20),
                borderSide: BorderSide(
                  color: AppColors.accent.withValues(alpha: 0.62),
                ),
              ),
            ),
          ),
          if (mediaUrl != null && mediaUrl!.trim().isNotEmpty) ...[
            const SizedBox(height: 14),
            Stack(
              children: [
                ClipRRect(
                  borderRadius: BorderRadius.circular(20),
                  child: Container(
                    width: double.infinity,
                    constraints: const BoxConstraints(
                      minHeight: 150,
                      maxHeight: 280,
                    ),
                    color: Colors.black,
                    child: (mediaContentType ?? '').toLowerCase().startsWith('video/')
                        ? NetworkVisualMedia(
                            url: mediaUrl!,
                            contentType: mediaContentType ?? 'video/mp4',
                            allowInlineVideo: true,
                            autoplay: false,
                            looping: true,
                            startMuted: true,
                            showControls: false,
                            allowPlaybackSpeedChanging: false,
                            showVideoBadge: true,
                            fit: BoxFit.contain,
                            placeholderLabel: 'Не удалось загрузить медиа',
                          )
                        : Center(
                            child: Image.network(
                              mediaUrl!,
                              fit: BoxFit.contain,
                              width: double.infinity,
                            ),
                          ),
                  ),
                ),
                Positioned(
                  right: 10,
                  top: 10,
                  child: Material(
                    color: Colors.black.withValues(alpha: 0.42),
                    shape: const CircleBorder(),
                    child: InkWell(
                      customBorder: const CircleBorder(),
                      onTap: saving ? null : onClearMedia,
                      child: const SizedBox(
                        width: 34,
                        height: 34,
                        child: Icon(
                          Icons.close_rounded,
                          color: Colors.white,
                          size: 20,
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ],
          const SizedBox(height: 14),
          Row(
            children: [
              SizedBox(
                width: 48,
                height: 48,
                child: OutlinedButton(
                  onPressed: saving ? null : onPickMedia,
                  style: OutlinedButton.styleFrom(
                    padding: EdgeInsets.zero,
                    foregroundColor: AppColors.textPrimary,
                    side: BorderSide(
                      color: AppColors.border.withValues(alpha: 0.74),
                    ),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(18),
                    ),
                  ),
                  child: Icon(
                    mediaUrl == null
                        ? Icons.attach_file_rounded
                        : Icons.attach_file_rounded,
                    size: 23,
                  ),
                ),
              ),
              const SizedBox(width: 12),
              if (onCancelEdit != null) ...[
                TextButton(
                  onPressed: saving ? null : onCancelEdit,
                  child: const Text('Отмена'),
                ),
                const SizedBox(width: 8),
              ],
              Flexible(
                fit: FlexFit.loose,
                child: FilledButton(
                  onPressed: saving ? null : onSubmit,
                  style: FilledButton.styleFrom(
                    padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 13),
                    minimumSize: const Size(0, 44),
                    backgroundColor: AppColors.accent.withValues(alpha: 0.84),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(18),
                    ),
                  ),
                  child: saving
                      ? const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(editing ? 'Сохранить' : 'Отправить всем'),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _AnnouncementHistoryCard extends StatelessWidget {
  final SystemAnnouncement item;
  final VoidCallback? onEdit;
  final VoidCallback onDelete;

  const _AnnouncementHistoryCard({
    required this.item,
    required this.onEdit,
    required this.onDelete,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(14),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.34),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.softStroke(0.10)),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Icon(
            Icons.campaign_rounded,
            color: AppColors.textPrimary,
            size: 24,
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Text(
              item.text,
              maxLines: 4,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: AppColors.textPrimary,
                height: 1.35,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          if (onEdit != null) ...[
            const SizedBox(width: 8),
            IconButton(
              onPressed: onEdit,
              icon: const Icon(Icons.edit_rounded),
              color: AppColors.textSecondary,
            ),
          ],
          const SizedBox(width: 4),
          IconButton(
            onPressed: onDelete,
            icon: const Icon(Icons.delete_outline_rounded),
            color: AppColors.textSecondary,
          ),
        ],
      ),
    );
  }
}

class _StateCard extends StatelessWidget {
  final IconData icon;
  final String text;

  const _StateCard({
    required this.icon,
    required this.text,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: AppColors.surface.withValues(alpha: 0.32),
        borderRadius: BorderRadius.circular(22),
        border: Border.all(color: AppColors.softStroke(0.08)),
      ),
      child: Column(
        children: [
          Icon(icon, color: AppColors.textSecondary, size: 34),
          const SizedBox(height: 10),
          Text(
            text,
            textAlign: TextAlign.center,
            style: const TextStyle(
              color: AppColors.textSecondary,
              height: 1.35,
            ),
          ),
        ],
      ),
    );
  }
}

class _CircleButton extends StatelessWidget {
  final IconData icon;
  final VoidCallback onTap;

  const _CircleButton({
    required this.icon,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.surface.withValues(alpha: 0.88),
      shape: const CircleBorder(),
      child: InkWell(
        customBorder: const CircleBorder(),
        onTap: onTap,
        child: SizedBox(
          width: 44,
          height: 44,
          child: Icon(icon, color: AppColors.textPrimary),
        ),
      ),
    );
  }
}