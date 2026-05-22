import 'package:flutter/material.dart';

import '../layout/inmoment_media_frame.dart';

class InMomentResponsiveContent extends StatelessWidget {
  final Widget child;
  final Alignment alignment;
  final EdgeInsetsGeometry padding;
  final double? maxWidth;

  const InMomentResponsiveContent({
    super.key,
    required this.child,
    this.alignment = Alignment.topCenter,
    this.padding = EdgeInsets.zero,
    this.maxWidth,
  });

  @override
  Widget build(BuildContext context) {
    final resolvedWidth = InMomentMediaFrame.resolveTabletContentWidth(
      MediaQuery.sizeOf(context).width,
    );

    final width = maxWidth == null
        ? resolvedWidth
        : resolvedWidth.clamp(0.0, maxWidth!).toDouble();

    return Align(
      alignment: alignment,
      child: SizedBox(
        width: width,
        child: Padding(
          padding: padding,
          child: child,
        ),
      ),
    );
  }
}