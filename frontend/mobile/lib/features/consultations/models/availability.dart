// Booking-availability models — mirrors
// PetCare.Application/DTOs/Consultations/AvailabilityDtos.cs.

/// One fixed one-hour slot of a working day ("09:00"–"10:00" …).
class AvailabilitySlot {
  /// Slot start, "HH:mm".
  final String start;

  /// Slot end — always start + 1 hour.
  final String end;
  final bool available;
  final List<String> availableVeterinarianIds;

  AvailabilitySlot({
    required this.start,
    required this.end,
    required this.available,
    this.availableVeterinarianIds = const [],
  });

  factory AvailabilitySlot.fromJson(Map<String, dynamic> json) {
    return AvailabilitySlot(
      start: json['start'] as String? ?? '',
      end: json['end'] as String? ?? '',
      available: json['available'] as bool? ?? false,
      availableVeterinarianIds:
          (json['availableVeterinarianIds'] as List<dynamic>?)
                  ?.map((e) => e.toString())
                  .toList() ??
              const [],
    );
  }
}

/// Slot-by-slot availability for one organization day.
class DayAvailability {
  /// "yyyy-MM-dd"
  final String date;
  final bool isPast;
  final List<AvailabilitySlot> slots;

  DayAvailability({required this.date, this.isPast = false, this.slots = const []});

  factory DayAvailability.fromJson(Map<String, dynamic> json) {
    return DayAvailability(
      date: json['date'] as String? ?? '',
      isPast: json['isPast'] as bool? ?? false,
      slots: (json['slots'] as List<dynamic>?)
              ?.map((e) => AvailabilitySlot.fromJson(e as Map<String, dynamic>))
              .toList() ??
          const [],
    );
  }
}

/// One day entry of the month availability overview.
class MonthAvailabilityDay {
  /// "yyyy-MM-dd"
  final String date;
  final bool available;
  final bool fullyBooked;
  final bool isPast;

  MonthAvailabilityDay({
    required this.date,
    this.available = false,
    this.fullyBooked = false,
    this.isPast = false,
  });

  /// Past days and fully-booked days cannot be picked.
  bool get selectable => !isPast && !fullyBooked;

  factory MonthAvailabilityDay.fromJson(Map<String, dynamic> json) {
    return MonthAvailabilityDay(
      date: json['date'] as String? ?? '',
      available: json['available'] as bool? ?? false,
      fullyBooked: json['fullyBooked'] as bool? ?? false,
      isPast: json['isPast'] as bool? ?? false,
    );
  }
}
