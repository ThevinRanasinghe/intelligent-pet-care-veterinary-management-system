class AppointmentSlot {
  final String id;
  final String veterinarianId;
  final String date;
  final String startTime;
  final String endTime;
  final String branch;
  final String status;

  AppointmentSlot({
    required this.id,
    required this.veterinarianId,
    required this.date,
    required this.startTime,
    required this.endTime,
    required this.branch,
    required this.status,
  });

  factory AppointmentSlot.fromJson(Map<String, dynamic> json) {
    return AppointmentSlot(
      id: json['id'] as String,
      veterinarianId: json['veterinarianId'] as String,
      date: json['date'] as String,
      startTime: json['startTime'] as String,
      endTime: json['endTime'] as String,
      branch: json['branch'] as String,
      status: json['status'] as String,
    );
  }
}

class Appointment {
  final String id;
  final String petId;
  final String veterinarianId;
  final String appointmentSlotId;
  final String scheduledStart;
  final String scheduledEnd;
  final String status;
  final String? notes;
  final String createdAt;
  final String updatedAt;

  Appointment({
    required this.id,
    required this.petId,
    required this.veterinarianId,
    required this.appointmentSlotId,
    required this.scheduledStart,
    required this.scheduledEnd,
    required this.status,
    this.notes,
    required this.createdAt,
    required this.updatedAt,
  });

  factory Appointment.fromJson(Map<String, dynamic> json) {
    return Appointment(
      id: json['id'] as String,
      petId: json['petId'] as String,
      veterinarianId: json['veterinarianId'] as String,
      appointmentSlotId: json['appointmentSlotId'] as String,
      scheduledStart: json['scheduledStart'] as String,
      scheduledEnd: json['scheduledEnd'] as String,
      status: json['status'] as String,
      notes: json['notes'] as String?,
      createdAt: json['createdAt'] as String,
      updatedAt: json['updatedAt'] as String,
    );
  }
}
