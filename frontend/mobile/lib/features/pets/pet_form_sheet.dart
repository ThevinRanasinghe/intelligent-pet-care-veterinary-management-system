import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import '../../core/theme/app_colors.dart';
import '../../core/widgets/app_text_field.dart';
import '../../core/widgets/bottom_sheet_container.dart';
import 'pet_provider.dart';
import 'models/pet.dart';

/// Beacon add/edit pet bottom sheet — replaces the old full-page form.
/// Fields follow CreatePetDto/UpdatePetDto (name, species, breed, gender,
/// dateOfBirth, weight, photoUrl, notes). Returns `true` when the pet was
/// saved successfully.
Future<bool?> showPetFormSheet(BuildContext context, {Pet? pet}) {
  return showAppBottomSheet<bool>(
    context,
    BottomSheetContainer(
      title: pet == null ? 'Add Pet' : 'Edit Pet',
      child: PetFormSheet(pet: pet),
    ),
  );
}

/// Scrollable pet form rendered inside [BottomSheetContainer].
class PetFormSheet extends StatefulWidget {
  /// Null → create mode; a pet → edit mode.
  final Pet? pet;

  const PetFormSheet({super.key, this.pet});

  @override
  State<PetFormSheet> createState() => _PetFormSheetState();
}

class _PetFormSheetState extends State<PetFormSheet> {
  final _formKey = GlobalKey<FormState>();
  late final TextEditingController _nameController;
  late final TextEditingController _breedController;
  late final TextEditingController _weightController;
  late final TextEditingController _photoController;
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
    _photoController = TextEditingController(text: pet?.photoUrl ?? '');
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
    _photoController.dispose();
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
      photoUrl: _photoController.text.trim().isEmpty
          ? null
          : _photoController.text.trim(),
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
    return SingleChildScrollView(
      child: Form(
        key: _formKey,
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AppTextField(
              label: 'Name *',
              hint: 'Your pet’s name',
              controller: _nameController,
              validator: (v) =>
                  v == null || v.trim().isEmpty ? 'Name is required' : null,
            ),
            const SizedBox(height: 16),
            _LabeledDropdown(
              label: 'Species *',
              value: _species,
              items: _speciesOptions,
              onChanged: (v) => setState(() => _species = v ?? 'Dog'),
            ),
            const SizedBox(height: 16),
            AppTextField(
              label: 'Breed',
              hint: 'e.g. Golden Retriever',
              controller: _breedController,
            ),
            const SizedBox(height: 16),
            _LabeledDropdown(
              label: 'Gender',
              value: _gender,
              items: _genderOptions,
              onChanged: (v) => setState(() => _gender = v ?? 'Unknown'),
            ),
            const SizedBox(height: 16),
            _LabeledField(
              label: 'Date of Birth',
              child: InkWell(
                onTap: _pickDate,
                borderRadius: BorderRadius.circular(12),
                child: Container(
                  width: double.infinity,
                  padding:
                      const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
                  decoration: BoxDecoration(
                    color: AppColors.surface,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(color: AppColors.line),
                  ),
                  child: Row(
                    children: [
                      Expanded(
                        child: Text(
                          _dateOfBirth == null
                              ? 'Select date'
                              : _dateOfBirth!
                                  .toIso8601String()
                                  .substring(0, 10),
                          style: TextStyle(
                            fontSize: 14,
                            color: _dateOfBirth == null
                                ? AppColors.neutral
                                : AppColors.black,
                          ),
                        ),
                      ),
                      const Icon(Icons.cake_outlined,
                          size: 18, color: AppColors.muted),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 16),
            AppTextField(
              label: 'Weight (kg)',
              hint: 'e.g. 4.5',
              controller: _weightController,
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
            AppTextField(
              label: 'Photo URL',
              hint: 'https://… (optional)',
              controller: _photoController,
              keyboardType: TextInputType.url,
            ),
            const SizedBox(height: 16),
            AppTextField(
              label: 'Notes',
              hint: 'Allergies, temperament, …',
              controller: _notesController,
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
    );
  }
}

/// Label above a widget — shared label style for non-text controls.
class _LabeledField extends StatelessWidget {
  final String label;
  final Widget child;

  const _LabeledField({required this.label, required this.child});

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          label,
          style: const TextStyle(
            fontSize: 12,
            fontWeight: FontWeight.w700,
            color: AppColors.black,
          ),
        ),
        const SizedBox(height: 6),
        child,
      ],
    );
  }
}

class _LabeledDropdown extends StatelessWidget {
  final String label;
  final String value;
  final List<String> items;
  final ValueChanged<String?> onChanged;

  const _LabeledDropdown({
    required this.label,
    required this.value,
    required this.items,
    required this.onChanged,
  });

  @override
  Widget build(BuildContext context) {
    return _LabeledField(
      label: label,
      child: DropdownButtonFormField<String>(
        initialValue: value,
        items: items
            .map((s) => DropdownMenuItem(value: s, child: Text(s)))
            .toList(),
        onChanged: onChanged,
        style: const TextStyle(fontSize: 14, color: AppColors.black),
        decoration: const InputDecoration(),
      ),
    );
  }
}
