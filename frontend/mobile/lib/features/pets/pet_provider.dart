import 'package:flutter/foundation.dart';
import '../../core/state/load_state.dart';

export '../../core/state/load_state.dart' show LoadState;
import '../../core/network/api_error.dart';
import 'pet_service.dart';
import 'models/pet.dart';


class PetProvider extends ChangeNotifier {
  final PetService _service;

  PetProvider(this._service);

  LoadState _listState = LoadState.idle;
  List<Pet> _pets = [];
  String _errorMessage = '';
  bool _saving = false;

  LoadState get listState => _listState;
  List<Pet> get pets => _pets;
  String get errorMessage => _errorMessage;
  bool get saving => _saving;

  Future<void> loadMyPets() async {
    _listState = LoadState.loading;
    _errorMessage = '';
    notifyListeners();
    try {
      _pets = await _service.getMyPets();
      _listState = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _listState = LoadState.error;
    } catch (e) {
      _errorMessage = e.toString();
      _listState = LoadState.error;
    }
    notifyListeners();
  }

  /// Creates (pet.id empty) or updates a pet. Returns null on success or
  /// the API error message on failure.
  Future<String?> savePet(Pet pet) async {
    _saving = true;
    notifyListeners();
    try {
      if (pet.id.isEmpty) {
        await _service.createPet(pet);
      } else {
        await _service.updatePet(pet);
      }
      await loadMyPets();
      return null;
    } on ApiError catch (e) {
      return _extractMessage(e);
    } catch (e) {
      return e.toString();
    } finally {
      _saving = false;
      notifyListeners();
    }
  }

  /// Returns null on success or the API error message on failure.
  Future<String?> deletePet(String id) async {
    _saving = true;
    notifyListeners();
    try {
      await _service.deletePet(id);
      await loadMyPets();
      return null;
    } on ApiError catch (e) {
      return _extractMessage(e);
    } catch (e) {
      return e.toString();
    } finally {
      _saving = false;
      notifyListeners();
    }
  }

  String _extractMessage(ApiError e) {
    final body = e.body;
    if (body is Map) {
      // FluentValidation failures arrive as {errors: {Field: [msg]}}.
      final errors = body['errors'];
      if (errors is Map && errors.isNotEmpty) {
        final first = errors.values.first;
        if (first is List && first.isNotEmpty) return first.first.toString();
      }
      return body['detail'] ?? body['message'] ?? body['title'] ?? e.message;
    }
    if (body is String) return body;
    return e.message;
  }
}
