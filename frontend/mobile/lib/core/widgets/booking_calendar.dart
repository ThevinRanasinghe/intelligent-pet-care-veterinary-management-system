import 'package:flutter/material.dart';
import '../motion/app_motion.dart';
import '../theme/app_colors.dart';
import '../theme/app_spacing.dart';
import 'pressable_scale.dart';

/// Availability state for one calendar day in the booking flow.
class CalendarDay {
  final bool selectable;
  final bool fullyBooked;
  final bool isPast;

  const CalendarDay({
    this.selectable = false,
    this.fullyBooked = false,
    this.isPast = false,
  });
}

/// Beacon booking calendar: Monday-first 7-column month grid with chevron
/// navigation. Selected days are yellow; past/fully-booked days render
/// disabled and muted. Month changes slide directionally (next → from
/// the right, previous → from the left).
class BookingCalendar extends StatefulWidget {
  final int year;
  final int month;

  /// 'yyyy-MM-dd' → availability. Missing entries render disabled.
  final Map<String, CalendarDay> days;
  final String? selectedDate;
  final ValueChanged<String> onSelect;
  final ValueChanged<int> onShiftMonth;

  const BookingCalendar({
    super.key,
    required this.year,
    required this.month,
    required this.days,
    required this.onSelect,
    required this.onShiftMonth,
    this.selectedDate,
  });

  static const _monthNames = [
    'January',
    'February',
    'March',
    'April',
    'May',
    'June',
    'July',
    'August',
    'September',
    'October',
    'November',
    'December',
  ];

  static const _weekdays = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];

  @override
  State<BookingCalendar> createState() => _BookingCalendarState();
}

class _BookingCalendarState extends State<BookingCalendar> {
  /// +1 when the month advanced, −1 when it moved back — drives the
  /// direction of the AnimatedSwitcher slide.
  int _monthDirection = 1;

  @override
  void didUpdateWidget(BookingCalendar oldWidget) {
    super.didUpdateWidget(oldWidget);
    final before = oldWidget.year * 12 + oldWidget.month;
    final after = widget.year * 12 + widget.month;
    if (after != before) _monthDirection = after > before ? 1 : -1;
  }

  @override
  Widget build(BuildContext context) {
    final year = widget.year;
    final month = widget.month;
    final daysInMonth = DateUtils.getDaysInMonth(year, month);
    final firstWeekday = DateTime(year, month, 1).weekday;
    final cellCount = (firstWeekday - 1) + daysInMonth;

    return Column(
      children: [
        Padding(
          padding:
              const EdgeInsets.symmetric(horizontal: AppSpacing.pageHorizontal),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              IconButton(
                icon: const Icon(Icons.chevron_left),
                tooltip: 'Previous month',
                onPressed: () => widget.onShiftMonth(-1),
              ),
              AnimatedSwitcher(
                duration: AppMotion.standard,
                transitionBuilder: (child, animation) => FadeTransition(
                  opacity: animation,
                  child: child,
                ),
                child: Text(
                  '${BookingCalendar._monthNames[month - 1]} $year',
                  key: ValueKey('$year-$month'),
                  style: const TextStyle(
                    fontSize: 15,
                    fontWeight: FontWeight.w800,
                    color: AppColors.black,
                  ),
                ),
              ),
              IconButton(
                icon: const Icon(Icons.chevron_right),
                tooltip: 'Next month',
                onPressed: () => widget.onShiftMonth(1),
              ),
            ],
          ),
        ),
        Padding(
          padding:
              const EdgeInsets.symmetric(horizontal: AppSpacing.pageHorizontal),
          child: Row(
            children: [
              for (final d in BookingCalendar._weekdays)
                Expanded(
                  child: Center(
                    child: Text(
                      d,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.caption,
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 4),
        Expanded(
          // Month grids swap directionally: next month slides in from
          // the right, previous from the left (~220 ms).
          child: AnimatedSwitcher(
            duration: const Duration(milliseconds: 220),
            transitionBuilder: (child, animation) {
              final incoming = child.key == ValueKey('grid-$year-$month');
              final begin =
                  Offset(0.08 * _monthDirection * (incoming ? 1 : -1), 0);
              return FadeTransition(
                opacity: animation,
                child: SlideTransition(
                  position: Tween(begin: begin, end: Offset.zero).animate(
                      CurvedAnimation(
                          parent: animation, curve: AppMotion.easeOut)),
                  child: child,
                ),
              );
            },
            child: GridView.builder(
              key: ValueKey('grid-$year-$month'),
              padding: const EdgeInsets.symmetric(
                  horizontal: AppSpacing.pageHorizontal, vertical: 4),
              gridDelegate: const SliverGridDelegateWithFixedCrossAxisCount(
                crossAxisCount: 7,
                childAspectRatio: 0.72,
              ),
              itemCount: cellCount,
              itemBuilder: (context, index) {
                if (index < firstWeekday - 1) {
                  return const SizedBox.shrink();
                }
                final day = index - (firstWeekday - 1) + 1;
                final dateStr =
                    '$year-${month.toString().padLeft(2, '0')}-${day.toString().padLeft(2, '0')}';
                final info = widget.days[dateStr] ?? const CalendarDay();
                final enabled = info.selectable;
                final selected = widget.selectedDate == dateStr;
                return PressableScale(
                  onTap: enabled ? () => widget.onSelect(dateStr) : null,
                  enabled: enabled,
                  child: AnimatedContainer(
                    key: Key('day-$dateStr'),
                    duration: const Duration(milliseconds: 180),
                    curve: AppMotion.easeOut,
                    margin: const EdgeInsets.all(2),
                    decoration: BoxDecoration(
                      color: selected
                          ? AppColors.primary
                          : enabled
                              ? AppColors.surface
                              : AppColors.neutralBg,
                      borderRadius: BorderRadius.circular(10),
                      border: Border.all(
                        color: selected
                            ? AppColors.primaryDark
                            : enabled
                                ? AppColors.line
                                : AppColors.neutralBorder,
                      ),
                    ),
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        AnimatedScale(
                          scale: selected ? 1.1 : 1.0,
                          duration: const Duration(milliseconds: 180),
                          curve: AppMotion.easeOut,
                          child: Text(
                            '$day',
                            style: TextStyle(
                              fontWeight: FontWeight.bold,
                              color: selected
                                  ? AppColors.black
                                  : enabled
                                      ? AppColors.black
                                      : AppColors.neutral,
                            ),
                          ),
                        ),
                        if (info.fullyBooked)
                          const FittedBox(
                            child: Padding(
                              padding: EdgeInsets.symmetric(horizontal: 2),
                              child: Text(
                                'Fully booked',
                                style: TextStyle(
                                    fontSize: 9, color: AppColors.dangerText),
                              ),
                            ),
                          )
                        else if (info.isPast)
                          const FittedBox(
                            child: Padding(
                              padding: EdgeInsets.symmetric(horizontal: 2),
                              child: Text(
                                'Past',
                                style: TextStyle(
                                    fontSize: 9, color: AppColors.neutral),
                              ),
                            ),
                          ),
                      ],
                    ),
                  ),
                );
              },
            ),
          ),
        ),
      ],
    );
  }
}
