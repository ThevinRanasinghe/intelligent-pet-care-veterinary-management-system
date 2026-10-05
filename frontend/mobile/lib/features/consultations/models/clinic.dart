/// Active organization shown in the owner booking flow.
/// Mirrors GET /lookups/organizations → [{id, name, city, address,
/// latitude, longitude}] — coordinates are null while the organization
/// has no stored location.
class Clinic {
  final String id;
  final String name;
  final String? city;
  final String? address;
  final double? latitude;
  final double? longitude;

  Clinic({
    required this.id,
    required this.name,
    this.city,
    this.address,
    this.latitude,
    this.longitude,
  });

  bool get hasLocation => latitude != null && longitude != null;

  String get addressLabel {
    final parts = [address, city].where((p) => p != null && p.isNotEmpty);
    return parts.isEmpty ? 'Address not available' : parts.join(', ');
  }

  factory Clinic.fromJson(Map<String, dynamic> json) {
    return Clinic(
      id: json['id'] as String,
      name: json['name'] as String? ?? '',
      city: json['city'] as String?,
      address: json['address'] as String?,
      latitude: (json['latitude'] as num?)?.toDouble(),
      longitude: (json['longitude'] as num?)?.toDouble(),
    );
  }
}
