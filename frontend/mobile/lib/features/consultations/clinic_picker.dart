import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../core/theme/app_colors.dart';
import 'models/clinic.dart';

/// Google Maps directions URL for a clinic pin.
Uri clinicDirectionsUri(Clinic clinic) => Uri.parse(
    'https://www.google.com/maps/dir/?api=1&destination=${clinic.latitude},${clinic.longitude}');

/// Clinic selection: a Google Map with one marker per organization that
/// has stored coordinates (orgs without coords are skipped on the map but
/// still listed), plus a compact scrollable list of every active clinic —
/// the list always works even when the map cannot render (e.g. no API
/// key configured).
class ClinicPicker extends StatefulWidget {
  final List<Clinic> clinics;
  final String? selectedId;
  final ValueChanged<Clinic> onSelected;

  /// Google Maps needs a configured API key on device; tests and
  /// constrained environments can hide the map and keep the list.
  final bool showMap;

  const ClinicPicker({
    super.key,
    required this.clinics,
    required this.onSelected,
    this.selectedId,
    this.showMap = true,
  });

  @override
  State<ClinicPicker> createState() => _ClinicPickerState();
}

class _ClinicPickerState extends State<ClinicPicker> {
  Clinic? _focused;

  void _openDirections(Clinic clinic) {
    launchUrl(clinicDirectionsUri(clinic),
        mode: LaunchMode.externalApplication);
  }

  @override
  Widget build(BuildContext context) {
    if (widget.clinics.isEmpty) {
      return const Center(
        child: Padding(
          padding: EdgeInsets.all(24),
          child: Text(
            'No PetCare clinics are currently available in this area.',
            textAlign: TextAlign.center,
          ),
        ),
      );
    }

    final mappable = widget.clinics.where((c) => c.hasLocation).toList();
    final markers = mappable
        .map((c) => Marker(
              markerId: MarkerId(c.id),
              position: LatLng(c.latitude!, c.longitude!),
              infoWindow: InfoWindow(title: c.name, snippet: c.addressLabel),
              onTap: () => setState(() => _focused = c),
            ))
        .toSet();

    final initial = mappable.isNotEmpty
        ? LatLng(
            mappable.map((c) => c.latitude!).reduce((a, b) => a + b) /
                mappable.length,
            mappable.map((c) => c.longitude!).reduce((a, b) => a + b) /
                mappable.length,
          )
        : const LatLng(7.8731, 80.7718); // Sri Lanka centre fallback

    return Column(
      children: [
        if (widget.showMap && mappable.isNotEmpty)
          SizedBox(
            height: 220,
            child: GoogleMap(
              initialCameraPosition: CameraPosition(target: initial, zoom: 8),
              markers: markers,
              myLocationButtonEnabled: false,
              zoomControlsEnabled: false,
            ),
          ),
        if (_focused != null)
          Card(
            margin: const EdgeInsets.all(8),
            color: AppColors.selectedBg,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(17),
              side: const BorderSide(color: AppColors.selectedBorder),
            ),
            child: Padding(
              padding: const EdgeInsets.all(12),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  Text(_focused!.name,
                      style: const TextStyle(
                          fontWeight: FontWeight.w800,
                          color: AppColors.black)),
                  Text(_focused!.addressLabel,
                      style: const TextStyle(
                          fontSize: 12, color: AppColors.muted)),
                  const SizedBox(height: 8),
                  Row(
                    children: [
                      Expanded(
                        child: FilledButton(
                          onPressed: () => widget.onSelected(_focused!),
                          child: const Text('Select Clinic'),
                        ),
                      ),
                      const SizedBox(width: 8),
                      OutlinedButton.icon(
                        onPressed: () => _openDirections(_focused!),
                        icon: const Icon(Icons.directions),
                        label: const Text('Get Directions'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        Expanded(
          child: ListView.builder(
            shrinkWrap: true,
            itemCount: widget.clinics.length,
            itemBuilder: (context, index) {
              final clinic = widget.clinics[index];
              final selected = clinic.id == widget.selectedId;
              return Card(
                margin: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                color: selected ? AppColors.selectedBg : null,
                shape: RoundedRectangleBorder(
                  borderRadius: BorderRadius.circular(17),
                  side: BorderSide(
                    color: selected
                        ? AppColors.selectedBorder
                        : AppColors.line,
                  ),
                ),
                child: ListTile(
                  leading: Icon(
                    selected
                        ? Icons.radio_button_checked
                        : Icons.radio_button_off,
                    color: selected ? AppColors.primaryDark : AppColors.neutral,
                  ),
                  title: Text(clinic.name),
                  subtitle: Text(clinic.addressLabel),
                  trailing: clinic.hasLocation
                      ? IconButton(
                          icon: const Icon(Icons.directions_outlined),
                          tooltip: 'Get Directions',
                          onPressed: () => _openDirections(clinic),
                        )
                      : null,
                  onTap: () => widget.onSelected(clinic),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
