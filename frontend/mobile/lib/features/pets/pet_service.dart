import '../../core/network/api_client.dart';
import '../../core/network/api_error.dart';
import 'models/pet.dart';

/// Owner-facing pet API. For a PetOwner caller the backend scopes
/// GET /pets to their own records and stamps OwnerId on create, so the
/// owner profile id is resolved once via GET /petowners (which returns a
/// single-element list containing the caller's own profile — the same
/// call the web owner flow uses) and passed through for completeness.
class PetService {
  final ApiClient _apiClient;

  PetService(this._apiClient);

  /// The signed-in owner's pets (server-side scoped for PetOwner).
  /// Pass [includeArchived] to also receive archived pets — the default
  /// list only contains active ones.
  Future<List<Pet>> getMyPets({bool includeArchived = false}) async {
    return _apiClient.getList<Pet>(
      includeArchived ? '/pets?includeArchived=true' : '/pets',
      fromJson: Pet.fromJson,
    );
  }

  Future<Pet> getPetById(String id) async {
    return _apiClient.get<Pet>('/pets/$id', fromJson: Pet.fromJson);
  }

  /// Resolves the caller's PetOwner profile id (GET /petowners returns a
  /// one-element list for PetOwner accounts). Returns null when the
  /// account has no owner profile yet.
  Future<String?> getMyOwnerId() async {
    final owners = await _apiClient.getList<Map<String, dynamic>>(
      '/petowners',
      fromJson: (json) => json,
    );
    if (owners.isEmpty) return null;
    return owners.first['id'] as String?;
  }

  Future<Pet> createPet(Pet draft) async {
    final ownerId =
        draft.ownerId.isNotEmpty ? draft.ownerId : await getMyOwnerId();
    return _apiClient.post<Pet>(
      '/pets',
      body: draft.toPayload(ownerId: ownerId ?? ''),
      fromJson: Pet.fromJson,
    );
  }

  Future<Pet> updatePet(Pet pet) async {
    return _apiClient.put<Pet>(
      '/pets/${pet.id}',
      body: pet.toPayload(),
      fromJson: Pet.fromJson,
    );
  }

  /// Removes the pet from the owner's active list without touching any
  /// consultation, examination, prescription or billing history.
  Future<Pet> archivePet(String id) async {
    return _apiClient.post<Pet>('/pets/$id/archive', fromJson: Pet.fromJson);
  }

  /// Returns an archived pet to the owner's active list.
  Future<Pet> restorePet(String id) async {
    return _apiClient.post<Pet>('/pets/$id/restore', fromJson: Pet.fromJson);
  }

  Future<void> deletePet(String id) async {
    try {
      await _apiClient.delete('/pets/$id');
    } on ApiError {
      rethrow;
    }
  }
}
