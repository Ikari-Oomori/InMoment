import 'dart:async';

import 'package:flutter/material.dart';

import '../../../core/theme/app_colors.dart';
import '../../../core/widgets/inmoment_glass_dialog.dart';
import '../api/gifs_api.dart';
import '../models/gif_search_item.dart';

Future<String?> showGifPickerSheet(BuildContext context) {
  return showInMomentGlassBottomSheet<String>(
    context: context,
    child: const _GifPickerSheet(),
  );
}

class _GifPickerSheet extends StatefulWidget {
  const _GifPickerSheet();

  @override
  State<_GifPickerSheet> createState() => _GifPickerSheetState();
}

class _GifPickerSheetState extends State<_GifPickerSheet> {
  final GifsApi _api = GifsApi();
  final TextEditingController _searchController =
      TextEditingController(text: 'cute');

  Timer? _debounce;
  bool _loading = true;
  String? _error;
  List<GifSearchItem> _items = const [];

  @override
  void initState() {
    super.initState();
    _search('cute');
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _searchController.dispose();
    super.dispose();
  }

  void _onQueryChanged(String value) {
    _debounce?.cancel();
    _debounce = Timer(const Duration(milliseconds: 360), () {
      _search(value);
    });
  }

  Future<void> _search(String query) async {
    setState(() {
      _loading = true;
      _error = null;
    });

    try {
      final result = await _api.search(query: query, limit: 24);

      if (!mounted) return;

      setState(() {
        _items = result;
        _loading = false;
      });
    } catch (_) {
      if (!mounted) return;

      setState(() {
        _items = const [];
        _loading = false;
        _error = 'Не удалось загрузить GIF';
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.fromLTRB(10, 0, 10, 12),
      child: ConstrainedBox(
        constraints: BoxConstraints(
          maxHeight: MediaQuery.of(context).size.height * 0.72,
        ),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Row(
              children: [
                Expanded(
                  child: SizedBox(
                    height: 48,
                    child: TextField(
                      controller: _searchController,
                      onChanged: _onQueryChanged,
                      style: const TextStyle(
                        color: AppColors.textPrimary,
                        fontSize: 16,
                      ),
                      decoration: InputDecoration(
                        hintText: 'Поиск GIF',
                        hintStyle: const TextStyle(
                          color: AppColors.textSecondary,
                          fontSize: 16,
                        ),
                        prefixIcon: const Icon(
                          Icons.search_rounded,
                          color: AppColors.textSecondary,
                        ),
                        filled: true,
                        fillColor: AppColors.surfaceGlass(0.42),
                        border: OutlineInputBorder(
                          borderRadius: BorderRadius.circular(18),
                          borderSide: BorderSide.none,
                        ),
                        contentPadding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 0,
                        ),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                _GifCloseButton(
                  onTap: () => Navigator.of(context).pop(),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Expanded(child: _buildBody()),
          ],
        ),
      ),
    );
  }

  Widget _buildBody() {
    if (_loading) {
      return const Center(
        child: CircularProgressIndicator(strokeWidth: 2),
      );
    }

    if (_error != null) {
      return Center(
        child: Text(
          _error!,
          style: const TextStyle(color: AppColors.textSecondary),
        ),
      );
    }

    if (_items.isEmpty) {
      return const Center(
        child: Text(
          'Ничего не найдено',
          style: TextStyle(color: AppColors.textSecondary),
        ),
      );
    }

    return GridView.builder(
      itemCount: _items.length,
      gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
        crossAxisCount: 4,
        mainAxisSpacing: 6,
        crossAxisSpacing: 6,
        childAspectRatio: 0.85,
      ),
      itemBuilder: (context, index) {
        final item = _items[index];

        return ClipRRect(
          borderRadius: BorderRadius.circular(10),
          child: Material(
            color: AppColors.surfaceGlass(0.28),
            child: InkWell(
              onTap: () => Navigator.of(context).pop(item.gifUrl),
              child: Image.network(
                item.previewUrl,
                fit: BoxFit.cover,
                filterQuality: FilterQuality.low,
                loadingBuilder: (context, child, loadingProgress) {
                  if (loadingProgress == null) return child;

                  return const Center(
                    child: SizedBox(
                      width: 18,
                      height: 18,
                      child: CircularProgressIndicator(strokeWidth: 2),
                    ),
                  );
                },
                errorBuilder: (_, _, _) {
                  return const Center(
                    child: Icon(
                      Icons.gif_box_outlined,
                      color: AppColors.textSecondary,
                    ),
                  );
                },
              ),
            ),
          ),
        );
      },
    );
  }
}
class _GifCloseButton extends StatelessWidget {
  final VoidCallback onTap;

  const _GifCloseButton({
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return Material(
      color: AppColors.white.withValues(alpha: 0.055),
      shape: const CircleBorder(),
      child: InkWell(
        onTap: onTap,
        customBorder: const CircleBorder(),
        child: const SizedBox(
          width: 48,
          height: 48,
          child: Icon(
            Icons.close_rounded,
            color: AppColors.textSecondary,
          ),
        ),
      ),
    );
  }
}