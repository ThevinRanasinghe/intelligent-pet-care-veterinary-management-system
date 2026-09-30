import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import 'pet_provider.dart';
import 'models/pet.dart';

/// Add/Edit pet form. Fields follow CreatePetDto/UpdatePetDto (name,
/// species, breed, gender, dateOfBirth, weight, notes — photoUrl omitted
/// since the app has no image upload).
class PetFormPage extends StatefulWidget {
  /// Null → create mode; a pet → edit mode.
  final Pet? pet;

  const PetFormPage({super.key, this.pet});

  @override
  State<PetFormPage> createState() => _PetFormPageState();
}

class _PetFormPageState extends State<PetFormPage> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _breedController;
  late final TextEditingController _weightController;
  late final TextEditingController _notesController;
  late String _species;
  late String _gender;
  DateTime? _dateOfBirth;
  bool _submitting = false;

  static const _speciesOptions = ['Dog', 'Cat', 'Bird', 'Rabbit', 'Other'];
  static const _genderOptions = ['Male', 'Female', 'Unknown'];

  bool get _isEdit => widget.pet != null;

  @override
  void initState() {
    super.initState();
    final pet = widget.pet;
    _nameController = TextEditingController(text: pet?.name ?? '');
    _breedController = TextEditingController(text: pet?.breed ?? '');
    _weightController = TextEditingController(
        text: pet?.weight != null ? pet!.weight.toString() : '');
    _notesController = TextEditingController(text: pet?.notes ?? '');
    _species = _speciesOptions.contains(pet?.species)
        ? pet!.species
        : (pet?.species.isNotEmpty == true ? pet!.species : 'Dog');
    if (!_speciesOptions.contains(_species)) {
      _species = 'Other';
    }
    _gender = _genderOptions.contains(pet?.gender) ? pet!.gender! : 'Unknown';
    _dateOfBirth =
        pet?.dateOfBirth != null ? DateTime.tryParse(pet!.dateOfBirth!) : null;
  }

  @override
  void dispose() {
    _nameController.dispose();
    _breedController.dispose();
    _weightController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _dateOfBirth ?? DateTime.now(),
      firstDate: DateTime(1990),
      lastDate: DateTime.now(),
    );
    if (picked != null) setState(() => _dateOfBirth = picked);
  }

  Future<void> _submit() async {
    if (!_formKey.currentState!.validate()) return;
    setState(() => _submitting = true);
    final pet = Pet(
      id: widget.pet?.id ?? '',
      ownerId: widget.pet?.ownerId ?? '',
      name: _nameController.text.trim(),
      species: _species,
      breed: _breedController.text.trim().isEmpty
          ? null
          : _breedController.text.trim(),
      gender: _gender == 'Unknown' ? null : _gender,
      dateOfBirth: _dateOfBirth?.toIso8601String().substring(0, 10),
      weight: double.tryParse(_weightController.text.trim()),
      notes: _notesController.text.trim().isEmpty
          ? null
          : _notesController.text.trim(),
    );
    final error = await context.read<PetProvider>().savePet(pet);
    if (!mounted) return;
    setState(() => _submitting = false);
    if (error == null) {
      Navigator.of(context).pop(true);
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(error), backgroundColor: AppColors.danger),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(_isEdit ? 'Edit Pet' : 'Add Pet')),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: _formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              TextFormField(
                controller: _nameController,
                decoration: const InputDecoration(
                  labelText: 'Name *',

                ),
                validator: (v) =>
                    v == null || v.trim().isEmpty ? 'Name is required' : null,
              ),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                initialValue: _species,
                decoration: const InputDecoration(
                  labelText: 'Species *',

                ),
                items: _speciesOptions
                    .map((s) => DropdownMenuItem(value: s, child: Text(s)))
                    .toList(),
                onChanged: (v) => setState(() => _species = v ?? 'Dog'),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _breedController,
                decoration: const InputDecoration(
                  labelText: 'Breed',

                ),
              ),
              const SizedBox(height: 16),
              DropdownButtonFormField<String>(
                initialValue: _gender,
                decoration: const InputDecoration(
                  labelText: 'Gender',

                ),
                items: _genderOptions
                    .map((g) => DropdownMenuItem(value: g, child: Text(g)))
                    .toList(),
                onChanged: (v) => setState(() => _gender = v ?? 'Unknown'),
              ),
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: _pickDate,
                icon: const Icon(Icons.cake_outlined),
                label: Text(
                  _dateOfBirth == null
                      ? 'Date of Birth'
                      : 'DOB: ${_dateOfBirth!.toIso8601String().substring(0, 10)}',
                ),
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _weightController,
                decoration: const InputDecoration(
                  labelText: 'Weight (kg)',

                ),
                keyboardType:
                    const TextInputType.numberWithOptions(decimal: true),
                validator: (v) {
                  if (v == null || v.trim().isEmpty) return null;
                  final w = double.tryParse(v.trim());
                  if (w == null || w < 0) return 'Enter a valid weight';
                  return null;
                },
              ),
              const SizedBox(height: 16),
              TextFormField(
                controller: _notesController,
                decoration: const InputDecoration(
                  labelText: 'Notes',

                ),
                maxLines: 3,
              ),
              const SizedBox(height: 24),
              FilledButton(
                onPressed: _submitting ? null : _submit,
                child: _submitting
                    ? const SizedBox(
                        height: 20,
                        width: 20,
                        child: CircularProgressIndicator(strokeWidth: 2),
                      )
                    : Text(_isEdit ? 'Save Changes' : 'Add Pet'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}
