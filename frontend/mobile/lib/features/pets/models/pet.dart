/// Mirrors backend PetDto (PetCare.Application/DTOs/Pets/PetDto.cs).
class Pet {
  final String id;
  final String ownerId;
  final String name;
  final String species;
  final String? breed;
  final String? gender;
  final String? dateOfBirth;
  final double? weight;
  final String? photoUrl;
  final String? notes;

  Pet({
    required this.id,
    required this.ownerId,
    required this.name,
    required this.species,
    this.breed,
    this.gender,
    this.dateOfBirth,
    this.weight,
    this.photoUrl,
    this.notes,
  });

  factory Pet.fromJson(Map<String, dynamic> json) {
    return Pet(
      id: json['id'] as String,
      ownerId: json['ownerId'] as String? ?? '',
      name: json['name'] as String,
      species: json['species'] as String,
      breed: json['breed'] as String?,
      gender: json['gender'] as String?,
      dateOfBirth: json['dateOfBirth'] as String?,
      weight: (json['weight'] as num?)?.toDouble(),
      photoUrl: json['photoUrl'] as String?,
      notes: json['notes'] as String?,
    );
  }

  /// Whole years since dateOfBirth — null when the DOB is unknown/invalid.
  int? get ageYears {
    if (dateOfBirth == null) return null;
    final dob = DateTime.tryParse(dateOfBirth!);
    if (dob == null) return null;
    final now = DateTime.now();
    var years = now.year - dob.year;
    if (now.month < dob.month ||
        (now.month == dob.month && now.day < dob.day)) {
      years--;
    }
    return years < 0 ? 0 : years;
  }

  String get ageLabel {
    final years = ageYears;
    if (years == null) return 'Age unknown';
    if (years == 0) return '<1 year old';
    return '$years ${years == 1 ? 'year' : 'years'} old';
  }

  /// Payload shared by POST /pets and PUT /pets/{id} (ownerId is only sent
  /// on create — the server stamps it for PetOwner callers anyway).
  Map<String, dynamic> toPayload({String? ownerId}) {
    return {
      if (ownerId != null) 'ownerId': ownerId,
      'name': name,
      'species': species,
      'breed': breed,
      'gender': gender,
      'dateOfBirth': dateOfBirth,
      'weight': weight,
      'photoUrl': photoUrl,
      'notes': notes,
    };
  }
}
