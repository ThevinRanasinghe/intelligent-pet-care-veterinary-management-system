import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:petcare_mobile/core/motion/app_motion.dart';
import 'package:petcare_mobile/core/theme/app_colors.dart';
import 'package:petcare_mobile/core/widgets/app_bottom_nav.dart';
import 'package:petcare_mobile/core/widgets/fade_slide_in.dart';
import 'package:petcare_mobile/core/widgets/pop_in.dart';
import 'package:petcare_mobile/core/widgets/pressable_scale.dart';
import 'package:petcare_mobile/core/widgets/selectable_card.dart';
import 'package:petcare_mobile/core/widgets/step_dots.dart';

Widget host(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  group('MotionPageRoute', () {
    testWidgets('forward push fades and slides the new page in',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: Builder(
          builder: (context) => FilledButton(
            onPressed: () => Navigator.of(context).push(
              MotionPageRoute(page: const Scaffold(body: Text('pushed page'))),
            ),
            child: const Text('go'),
          ),
        ),
      ));

      await tester.tap(find.text('go'));
      // Mid-transition: a FadeTransition drives the incoming page.
      await tester.pump(const Duration(milliseconds: 80));
      expect(find.byType(FadeTransition), findsWidgets);

      await tester.pumpAndSettle();
      expect(find.text('pushed page'), findsOneWidget);
    });

    testWidgets('route duration matches the standard motion token',
        (tester) async {
      final route =
          MotionPageRoute<void>(page: const Scaffold(body: Text('x')));
      expect(route.transitionDuration, AppMotion.standard);
      expect(
          route.reverseTransitionDuration, const Duration(milliseconds: 220));
    });
  });

  group('FadeSlideIn', () {
    testWidgets('starts hidden and settles fully visible', (tester) async {
      await tester.pumpWidget(host(const FadeSlideIn(child: Text('hello'))));

      // t = 0 — opacity has not run yet.
      var opacity = tester.widget<Opacity>(find.byType(Opacity).first);
      expect(opacity.opacity, 0.0);

      await tester.pumpAndSettle();
      opacity = tester.widget<Opacity>(find.byType(Opacity).first);
      expect(opacity.opacity, 1.0);
      expect(find.text('hello'), findsOneWidget);
    });

    testWidgets('explicit delay keeps the child hidden until it elapses',
        (tester) async {
      await tester.pumpWidget(host(const FadeSlideIn(
        delay: Duration(milliseconds: 300),
        child: Text('delayed'),
      )));

      await tester.pump(const Duration(milliseconds: 100));
      var opacity = tester.widget<Opacity>(find.byType(Opacity).first);
      expect(opacity.opacity, 0.0);

      // pumpAndSettle does not wait on pending timers — advance the
      // fake clock past the delay, then settle the animation.
      await tester.pump(const Duration(milliseconds: 300));
      await tester.pumpAndSettle();
      opacity = tester.widget<Opacity>(find.byType(Opacity).first);
      expect(opacity.opacity, 1.0);
    });

    testWidgets('reduced-motion flag renders the child instantly',
        (tester) async {
      await tester.pumpWidget(const MediaQuery(
        data: MediaQueryData(disableAnimations: true),
        child: MaterialApp(
          home: Scaffold(
            body: FadeSlideIn(
                delay: Duration(milliseconds: 300), child: Text('now')),
          ),
        ),
      ));
      // No Opacity wrapper — the child is visible from the first frame.
      expect(find.text('now'), findsOneWidget);
      expect(find.byType(Opacity), findsNothing);
    });
  });

  group('PopIn', () {
    testWidgets('honours its delay then pops to scale 1', (tester) async {
      await tester.pumpWidget(host(const PopIn(
        delay: Duration(milliseconds: 200),
        child: Text('pop'),
      )));
      expect(find.text('pop'), findsOneWidget);
      await tester.pump(const Duration(milliseconds: 250));
      await tester.pumpAndSettle();
      final scale = tester.widget<Transform>(find.byType(Transform).first);
      // Settled transform is identity scale.
      expect(scale.transform.getMaxScaleOnAxis(), closeTo(1.0, 0.001));
    });
  });

  group('SelectableCard', () {
    testWidgets('selection swaps to the filled check icon', (tester) async {
      var selected = false;
      await tester.pumpWidget(StatefulBuilder(
        builder: (context, setState) => MaterialApp(
          home: Scaffold(
            body: SelectableCard(
              selected: selected,
              onTap: () => setState(() => selected = !selected),
              child: const Text('option'),
            ),
          ),
        ),
      ));

      expect(find.byIcon(Icons.circle_outlined), findsOneWidget);
      await tester.tap(find.text('option'));
      await tester.pumpAndSettle();
      expect(find.byIcon(Icons.check_circle), findsOneWidget);
    });
  });

  group('StepDots', () {
    testWidgets('advancing a step animates the dot to a check', (tester) async {
      var step = 0;
      await tester.pumpWidget(StatefulBuilder(
        builder: (context, setState) => MaterialApp(
          home: Scaffold(
            body: Column(
              children: [
                StepDots(currentStep: step, labels: const ['A', 'B', 'C']),
                TextButton(
                  onPressed: () => setState(() => step = 1),
                  child: const Text('advance'),
                ),
              ],
            ),
          ),
        ),
      ));

      expect(find.byIcon(Icons.check), findsNothing);
      await tester.tap(find.text('advance'));
      await tester.pumpAndSettle();
      // Step 0 is now completed — number swapped to a check icon.
      expect(find.byIcon(Icons.check), findsOneWidget);
    });
  });

  group('AppBottomNav active animation', () {
    testWidgets('switching index moves the yellow capsule', (tester) async {
      var index = 0;
      await tester.pumpWidget(StatefulBuilder(
        builder: (context, setState) => MaterialApp(
          home: Scaffold(
            bottomNavigationBar: AppBottomNav(
              currentIndex: index,
              onSelected: (i) => setState(() => index = i),
              items: const [
                AppNavItem(icon: Icons.home_outlined, label: 'Home'),
                AppNavItem(icon: Icons.pets_outlined, label: 'Pets'),
              ],
            ),
          ),
        ),
      ));

      await tester.tap(find.text('Pets'));
      await tester.pumpAndSettle();
      // The capsule behind 'Pets' is now the yellow active pill.
      final capsule = tester.widget<AnimatedContainer>(
        find.ancestor(
            of: find.text('Pets'), matching: find.byType(AnimatedContainer)),
      );
      expect((capsule.decoration! as BoxDecoration).color, AppColors.primary);
    });
  });

  group('PressableScale', () {
    testWidgets('still fires onTap after the motion change', (tester) async {
      var tapped = false;
      await tester.pumpWidget(host(
        PressableScale(
          onTap: () => tapped = true,
          child: const Text('press'),
        ),
      ));
      await tester.tap(find.text('press'));
      expect(tapped, isTrue);
    });
  });
}
