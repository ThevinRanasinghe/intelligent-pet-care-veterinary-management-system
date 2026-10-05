import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:petcare_mobile/core/widgets/app_bottom_nav.dart';
import 'package:petcare_mobile/core/widgets/app_button.dart';
import 'package:petcare_mobile/core/widgets/booking_calendar.dart';
import 'package:petcare_mobile/core/widgets/dark_action_card.dart';
import 'package:petcare_mobile/core/widgets/filter_pills.dart';
import 'package:petcare_mobile/core/widgets/pop_in.dart';
import 'package:petcare_mobile/core/widgets/segmented_control.dart';
import 'package:petcare_mobile/core/widgets/status_badge.dart';
import 'package:petcare_mobile/core/widgets/time_slot_grid.dart';
import 'package:petcare_mobile/core/widgets/top_bar.dart';

Widget host(Widget child) => MaterialApp(home: Scaffold(body: child));

void main() {
  group('AppBottomNav', () {
    testWidgets('renders all items and reports taps', (tester) async {
      var tapped = -1;
      await tester.pumpWidget(host(AppBottomNav(
        currentIndex: 0,
        onSelected: (i) => tapped = i,
        items: const [
          AppNavItem(icon: Icons.home_outlined, label: 'Home'),
          AppNavItem(icon: Icons.pets_outlined, label: 'My Pets'),
          AppNavItem(icon: Icons.event_outlined, label: 'Appointments'),
          AppNavItem(icon: Icons.receipt_long_outlined, label: 'Bills'),
          AppNavItem(icon: Icons.person_outline, label: 'Profile'),
        ],
      )));

      for (final label in [
        'Home',
        'My Pets',
        'Appointments',
        'Bills',
        'Profile'
      ]) {
        expect(find.text(label), findsOneWidget);
      }
      await tester.tap(find.text('Bills'));
      expect(tapped, 3);
    });
  });

  group('TopBar', () {
    testWidgets('centres the title and shows a back button when poppable',
        (tester) async {
      await tester.pumpWidget(MaterialApp(
        home: Builder(
          builder: (context) => FilledButton(
            onPressed: () => Navigator.of(context).push(
              MaterialPageRoute(
                builder: (_) =>
                    const Scaffold(appBar: TopBar(title: 'Details')),
              ),
            ),
            child: const Text('go'),
          ),
        ),
      ));
      await tester.tap(find.text('go'));
      await tester.pumpAndSettle();
      expect(find.text('Details'), findsOneWidget);
      expect(find.byIcon(Icons.arrow_back_ios_new), findsOneWidget);
    });
  });

  group('AppSegmentedControl', () {
    testWidgets('selects segments by tap', (tester) async {
      var selected = 1;
      await tester.pumpWidget(host(StatefulBuilder(
        builder: (context, setState) => AppSegmentedControl(
          labels: const ['Pending', 'Upcoming', 'History'],
          selectedIndex: selected,
          onSelected: (i) => setState(() => selected = i),
        ),
      )));
      await tester.tap(find.text('History'));
      await tester.pumpAndSettle();
      expect(selected, 2);
    });
  });

  group('FilterPills', () {
    testWidgets('renders emoji pills and reports selection', (tester) async {
      var selected = -1;
      await tester.pumpWidget(host(FilterPills(
        items: const [
          FilterPillItem('Max', emoji: '🐶'),
          FilterPillItem('Luna', emoji: '🐱'),
        ],
        selectedIndex: 0,
        onSelected: (i) => selected = i,
      )));
      expect(find.text('Max'), findsOneWidget);
      expect(find.text('🐶'), findsOneWidget);
      await tester.tap(find.text('Luna'));
      expect(selected, 1);
    });
  });

  group('TimeSlotGrid', () {
    testWidgets('disabled slots do not fire onSelected', (tester) async {
      String? picked;
      await tester.pumpWidget(host(TimeSlotGrid(
        slots: const [
          TimeSlot(label: '09:00 – 10:00', value: '09:00'),
          TimeSlot(label: '10:00 – 11:00', value: '10:00', available: false),
        ],
        onSelected: (v) => picked = v,
      )));
      await tester.tap(find.text('10:00 – 11:00'));
      expect(picked, isNull);
      await tester.tap(find.text('09:00 – 10:00'));
      expect(picked, '09:00');
    });
  });

  group('BookingCalendar', () {
    testWidgets('renders weekday header and marks the selected day',
        (tester) async {
      final now = DateTime.now();
      final d15 = '${now.year}-${now.month.toString().padLeft(2, '0')}-15';
      String? selected;
      await tester.pumpWidget(host(SizedBox(
        height: 500,
        child: BookingCalendar(
          year: now.year,
          month: now.month,
          days: {d15: const CalendarDay(selectable: true)},
          onSelect: (d) => selected = d,
          onShiftMonth: (_) {},
        ),
      )));
      for (final d in ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun']) {
        expect(find.text(d), findsOneWidget);
      }
      await tester.ensureVisible(find.byKey(Key('day-$d15')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(Key('day-$d15')));
      expect(selected, d15);
    });
  });

  group('DarkActionCard', () {
    testWidgets('shows eyebrow/title and fires onTap', (tester) async {
      var tapped = 0;
      await tester.pumpWidget(host(DarkActionCard(
        eyebrow: 'Book a visit',
        title: 'Schedule a vet appointment',
        subtitle: '5 quick steps',
        onTap: () => tapped++,
      )));
      expect(find.text('BOOK A VISIT'), findsOneWidget);
      expect(find.text('Schedule a vet appointment'), findsOneWidget);
      await tester.tap(find.text('Schedule a vet appointment'));
      expect(tapped, 1);
    });
  });

  group('StatusBadge', () {
    testWidgets('renders a status dot with the label', (tester) async {
      await tester.pumpWidget(host(const StatusBadge('Confirmed')));
      expect(find.text('Confirmed'), findsOneWidget);
      // 6px status dot per the Beacon spec.
      expect(
        find.byWidgetPredicate((w) =>
            w is Container &&
            w.constraints?.maxWidth == 6 &&
            w.constraints?.maxHeight == 6),
        findsOneWidget,
      );
    });
  });

  group('AppButton', () {
    testWidgets('shows a spinner and disables itself while loading',
        (tester) async {
      var tapped = 0;
      await tester.pumpWidget(host(AppButton(
        label: 'Sign In',
        loading: true,
        onPressed: () => tapped++,
      )));
      expect(find.byType(CircularProgressIndicator), findsOneWidget);
      await tester.tap(find.byType(AppButton), warnIfMissed: false);
      expect(tapped, 0);
    });
  });

  group('PopIn', () {
    testWidgets('renders its child', (tester) async {
      await tester.pumpWidget(host(const PopIn(child: Text('Popped'))));
      expect(find.text('Popped'), findsOneWidget);
      await tester.pumpAndSettle();
    });
  });
}
