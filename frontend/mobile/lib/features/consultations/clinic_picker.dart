import 'package:flutter/foundation.dart'
    show defaultTargetPlatform, kIsWeb, TargetPlatform;
import 'package:flutter/material.dart';
import 'package:google_maps_flutter/google_maps_flutter.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../core/config/maps_config.dart';
import '../../core/maps/google_maps_loader.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/list_icon_tile.dart';
import '../../core/widgets/selectable_card.dart';
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

  /// On web the Maps JS SDK loads on demand (no key in index.html); the map
  /// is only rendered once the SDK reports loaded.
  bool _webMapsReady = false;

  @override
  void initState() {
    super.initState();
    if (kIsWeb) {
      ensureGoogleMapsLoaded(MapsConfig.googleMapsApiKey).then((ready) {
        if (ready && mounted) setState(() => _webMapsReady = true);
      });
    }
  }

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
        if (widget.showMap &&
            mappable.isNotEmpty &&
            // GoogleMap ships Android/iOS/web implementations — the list
            // below remains the working picker on desktop, and on web until
            // the Maps JS SDK has loaded.
            (kIsWeb
                ? _webMapsReady
                : (defaultTargetPlatform == TargetPlatform.android ||
                    defaultTargetPlatform == TargetPlatform.iOS)))
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
          AppCard(
            margin: const EdgeInsets.symmetric(
                horizontal: AppSpacing.pageHorizontal, vertical: 6),
            color: AppColors.selectedBg,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(_focused!.name,
                    style: const TextStyle(
                        fontWeight: FontWeight.w800, color: AppColors.black)),
                Text(_focused!.addressLabel,
                    style:
                        const TextStyle(fontSize: 12, color: AppColors.muted)),
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
        // Clinic cards: soft-yellow icon tile + name/address; the selected
        // card animates to a yellow ring + tint + popping check.
        Expanded(
          child: ListView.builder(
            shrinkWrap: true,
            padding: const EdgeInsets.symmetric(
                horizontal: AppSpacing.pageHorizontal, vertical: 4),
            itemCount: widget.clinics.length,
            itemBuilder: (context, index) {
              final clinic = widget.clinics[index];
              final selected = clinic.id == widget.selectedId;
              return FadeSlideIn(
                delay: Duration(milliseconds: 50 * index.clamp(0, 6)),
                distance: 8,
                child: SelectableCard(
                  margin: const EdgeInsets.only(bottom: AppSpacing.xs),
                  selected: selected,
                  onTap: () => widget.onSelected(clinic),
                  child: Row(
                    children: [
                      const ListIconTile(
                          icon: Icons.local_hospital_outlined, size: 44),
                      const SizedBox(width: AppSpacing.sm),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              clinic.name,
                              style: const TextStyle(
                                fontSize: 14,
                                fontWeight: FontWeight.w800,
                                color: AppColors.black,
                              ),
                            ),
                            Text(
                              clinic.addressLabel,
                              style: const TextStyle(
                                  fontSize: 12, color: AppColors.muted),
                            ),
                          ],
                        ),
                      ),
                      if (clinic.hasLocation)
                        IconButton(
                          icon: const Icon(Icons.directions_outlined,
                              size: 20, color: AppColors.muted),
                          tooltip: 'Get Directions',
                          onPressed: () => _openDirections(clinic),
                        ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
      ],
    );
  }
}
