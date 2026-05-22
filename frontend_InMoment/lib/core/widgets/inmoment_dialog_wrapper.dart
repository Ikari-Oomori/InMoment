import 'package:flutter/material.dart';

import '../layout/inmoment_media_frame.dart';

class InMomentDialogWrapper extends StatelessWidget {
  final Widget child;
  final double? maxWidth;

  const InMomentDialogWrapper({
    super.key,
    required this.child,
    this.maxWidth,
  });

  @override
  Widget build(BuildContext context) {
    final resolvedWidth = InMomentMediaFrame.resolveDialogWidth(
      MediaQuery.sizeOf(context).width,
    );

    final width = maxWidth == null
        ? resolvedWidth
        : resolvedWidth.clamp(0.0, maxWidth!).toDouble();

    return Center(
      child: SizedBox(
        width: width,
        child: child,
      ),
    );
  }
}