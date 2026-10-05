import 'package:flutter/material.dart';
import 'package:provider/provider.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../core/theme/app_colors.dart';
import '../../core/theme/app_spacing.dart';
import '../../core/widgets/app_button.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/detail_row.dart';
import '../../core/widgets/fade_slide_in.dart';
import '../../core/widgets/section_header.dart';
import '../../core/widgets/status_badge.dart';
import '../../core/widgets/top_bar.dart';
import 'consultation_provider.dart';
import 'models/clinic.dart';
import 'models/consultation_request.dart';

/// Owner-facing consultation request detail. Get Directions resolves the
/// request's organizationId against /lookups/organizations — the button
/// is omitted when the clinic has no stored coordinates.
class OwnerConsultationDetailPage extends StatefulWidget {
  final ConsultationRequest request;

  const OwnerConsultationDetailPage({super.key, required this.request});

  @override
  State<OwnerConsultationDetailPage> createState() =>
      _OwnerConsultationDetailPageState();
}

class _OwnerConsultationDetailPageState
    extends State<OwnerConsultationDetailPage> {
  Clinic? _clinic;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _resolveClinic());
  }

  Future<void> _resolveClinic() async {
    final orgId = widget.request.organizationId;
    if (orgId == null) return;
    final provider = context.read<ConsultationProvider>();
    if (provider.clinics.isEmpty) {
      await provider.loadClinics();
    }
    if (!mounted) return;
    Clinic? match;
    for (final c in provider.clinics) {
      if (c.id == orgId && c.hasLocation) match = c;
    }
    setState(() => _clinic = match);
  }

  Future<void> _openDirections() async {
    final clinic = _clinic;
    if (clinic == null) return;
    final uri = Uri.parse(
      'https://www.google.com/maps/dir/?api=1&destination=${clinic.latitude},${clinic.longitude}',
    );
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  @override
  Widget build(BuildContext context) {
    final r = widget.request;
    return Scaffold(
      appBar: const TopBar(title: 'Consultation Request'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
            AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
        children: [
          FadeSlideIn(
            child: AppCard(
              padding: const EdgeInsets.all(20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: Text(
                          r.petName ?? 'Consultation request',
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.4,
                            color: AppColors.black,
                          ),
                        ),
                      ),
                      StatusBadge(r.status),
                    ],
                  ),
                  if (r.preferredDateShort != null ||
                      r.preferredTimeShort != null) ...[
                    const SizedBox(height: 4),
                    Text(
                      '${r.preferredDateShort ?? 'Date TBC'}'
                      '${r.preferredTimeShort != null ? ' · ${r.preferredTimeShort}' : ''}',
                      style:
                          const TextStyle(fontSize: 12, color: AppColors.muted),
                    ),
                  ],
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          const FadeSlideIn(
            delay: Duration(milliseconds: 80),
            distance: 8,
            child: SectionHeader('Details'),
          ),
          const SizedBox(height: 8),
          FadeSlideIn(
            delay: const Duration(milliseconds: 120),
            distance: 10,
            child: AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DetailRow('Pet', value: r.petName ?? r.petId),
                  if (r.organizationName != null)
                    DetailRow('Clinic', value: r.organizationName!),
                  if (_clinic != null)
                    DetailRow('Clinic address', value: _clinic!.addressLabel),
                  if (r.preferredDateShort != null)
                    DetailRow('Date', value: r.preferredDateShort!),
                  if (r.preferredTimeShort != null)
                    DetailRow('Time', value: r.preferredTimeShort!),
                  if (r.requestType == 'FollowUp') ...[
                    const DetailRow('Type', value: 'Follow-up request'),
                    if (r.requestedByVeterinarianName != null)
                      DetailRow('Requested by',
                          value: r.requestedByVeterinarianName!),
                  ],
                  DetailRow('Status', child: StatusBadge(r.status)),
                  if (r.agentWorkflowStatusLabel != null)
                    DetailRow('AI workflow',
                        child: StatusBadge(r.agentWorkflowStatusLabel!)),
                  if (r.symptoms.isNotEmpty)
                    DetailRow('Symptoms', value: r.symptoms),
                  if (r.additionalNotes != null &&
                      r.additionalNotes!.isNotEmpty)
                    DetailRow('Notes', value: r.additionalNotes!),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          if (_clinic != null)
            FadeSlideIn(
              delay: const Duration(milliseconds: 180),
              distance: 8,
              child: AppButton(
                label: 'Get Directions',
                icon: Icons.directions,
                onPressed: _openDirections,
              ),
            ),
        ],
      ),
    );
  }
}
