import 'package:flutter/foundation.dart';
import '../../core/network/api_error.dart';
import '../../core/state/load_state.dart';
import 'history_service.dart';
import 'models/medical_history_entry.dart';

export '../../core/state/load_state.dart' show LoadState;

/// Loads the clinical timeline for one pet at a time. Re-requesting the
/// pet already loaded is a no-op unless [force] is passed.
class HistoryProvider extends ChangeNotifier {
  final HistoryService _service;

  HistoryProvider(this._service);

  LoadState _state = LoadState.idle;
  List<MedicalHistoryEntry> _entries = [];
  String _errorMessage = '';
  String? _loadedPetId;

  LoadState get state => _state;
  List<MedicalHistoryEntry> get entries => _entries;
  String get errorMessage => _errorMessage;
  String? get loadedPetId => _loadedPetId;

  Future<void> loadForPet(String petId, {bool force = false}) async {
    if (!force && _state == LoadState.success && _loadedPetId == petId) {
      return;
    }
    _state = LoadState.loading;
    _errorMessage = '';
    _loadedPetId = petId;
    notifyListeners();
    try {
      _entries = await _service.getPetHistory(petId);
      _state = LoadState.success;
    } on ApiError catch (e) {
      _errorMessage = _extractMessage(e);
      _state = LoadState.error;
    } catch (e) {
      _errorMessage = 'Unable to load medical history. Please try again.';
      _state = LoadState.error;
    }
    notifyListeners();
  }

  String _extractMessage(ApiError e) {
    final body = e.body;
    if (body is Map) {
      return body['detail']?.toString() ??
          body['message']?.toString() ??
          body['title']?.toString() ??
          'Unable to load medical history. Please try again.';
    }
    return 'Unable to load medical history. Please try again.';
  }
}
