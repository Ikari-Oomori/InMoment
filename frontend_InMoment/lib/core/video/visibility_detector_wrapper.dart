import 'package:flutter/widgets.dart';
import 'package:visibility_detector/visibility_detector.dart';

class VisibilityDetectorWrapper extends StatelessWidget {
  final Widget child;
  final VoidCallback onVisible;
  final VoidCallback onHidden;

  const VisibilityDetectorWrapper({
    super.key,
    required this.child,
    required this.onVisible,
    required this.onHidden,
  });

  @override
  Widget build(BuildContext context) {
    return VisibilityDetector(
      key: Key(child.hashCode.toString()),
      onVisibilityChanged: (info) {
        if (info.visibleFraction > 0.6) {
          onVisible();
        } else {
          onHidden();
        }
      },
      child: child,
    );
  }
}