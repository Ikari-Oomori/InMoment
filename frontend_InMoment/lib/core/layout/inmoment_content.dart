import 'package:flutter/material.dart';

class InMomentContent extends StatelessWidget {
  final Widget child;

  const InMomentContent({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(
          maxWidth: 760,
          minWidth: 0,
        ),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 10),
          child: child,
        ),
      ),
    );
  }
}