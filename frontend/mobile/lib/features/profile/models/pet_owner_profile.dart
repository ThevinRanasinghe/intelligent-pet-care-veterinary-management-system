/// Mirrors backend PetOwnerDto
/// (PetCare.Application/DTOs/PetOwners/PetOwnerDto.cs).
class PetOwnerProfile {
  final String id;
  final String fullName;
  final String email;
  final String? phoneNumber;
  final String? address;

  PetOwnerProfile({
    required this.id,
    required this.fullName,
    required this.email,
    this.phoneNumber,
    this.address,
  });

  factory PetOwnerProfile.fromJson(Map<String, dynamic> json) {
    return PetOwnerProfile(
      id: json['id'] as String,
      fullName: json['fullName'] as String? ?? '',
      email: json['email'] as String? ?? '',
      phoneNumber: json['phoneNumber'] as String?,
      address: json['address'] as String?,
    );
  }
}
