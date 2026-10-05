import '../../core/network/api_client.dart';
import 'models/pet_owner_profile.dart';

/// Owner profile lookups. GET /petowners is scoped server-side for a
/// PetOwner token and returns a single-element list with the caller's
/// own profile — the same resolution the web owner flow uses
/// (ownerService.getAllOwners → owners[0]).
class ProfileService {
  final ApiClient _apiClient;

  ProfileService(this._apiClient);

  Future<PetOwnerProfile?> getMyOwnerProfile() async {
    final owners = await _apiClient.getList<PetOwnerProfile>(
      '/petowners',
      fromJson: PetOwnerProfile.fromJson,
    );
    return owners.isEmpty ? null : owners.first;
  }
}
