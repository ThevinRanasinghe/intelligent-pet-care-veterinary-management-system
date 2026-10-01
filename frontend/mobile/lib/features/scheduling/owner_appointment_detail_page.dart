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
import '../../core/widgets/status_timeline.dart';
import '../../core/widgets/top_bar.dart';
import '../consultations/consultation_provider.dart';
import '../consultations/models/clinic.dart';
import 'models/appointment_slot.dart';

/// Owner-facing appointment detail. The appointment payload carries no
/// organization fields, so the clinic (for the Get Directions button) is
/// resolved through the linked consultation's organizationId →
/// /lookups/organizations. The button is omitted when no clinic
/// coordinates can be resolved.
class OwnerAppointmentDetailPage extends StatefulWidget {
  final Appointment appointment;

  const OwnerAppointmentDetailPage({super.key, required this.appointment});

  @override
  State<OwnerAppointmentDetailPage> createState() =>
      _OwnerAppointmentDetailPageState();
}

class _OwnerAppointmentDetailPageState
    extends State<OwnerAppointmentDetailPage> {
  Clinic? _clinic;
  bool _clinicResolved = false;

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) => _resolveClinic());
  }

  Future<void> _resolveClinic() async {
    final consultationId = widget.appointment.consultationRequestId;
    Clinic? clinic;
    if (consultationId != null && consultationId.isNotEmpty) {
      clinic = await context
          .read<ConsultationProvider>()
          .clinicForAppointment(consultationId);
    }
    if (mounted) {
      setState(() {
        _clinic = clinic;
        _clinicResolved = true;
      });
    }
  }

  Future<void> _openDirections() async {
    final clinic = _clinic;
    if (clinic == null) return;
    final uri = Uri.parse(
      'https://www.google.com/maps/dir/?api=1&destination=${clinic.latitude},${clinic.longitude}',
    );
    await launchUrl(uri, mode: LaunchMode.externalApplication);
  }

  String _date(String iso) => iso.length >= 10 ? iso.substring(0, 10) : iso;
  String _time(String iso) => iso.length >= 16 ? iso.substring(11, 16) : '';

  @override
  Widget build(BuildContext context) {
    final appt = widget.appointment;
    return Scaffold(
      appBar: const TopBar(title: 'Appointment Details'),
      body: ListView(
        padding: const EdgeInsets.fromLTRB(AppSpacing.pageHorizontal,
            AppSpacing.md, AppSpacing.pageHorizontal, AppSpacing.xl),
        children: [
          // Header card: pet + date/time + status badge.
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
                          appt.petName ?? 'Your pet',
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.4,
                            color: AppColors.black,
                          ),
                        ),
                      ),
                      StatusBadge(appt.status),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    '${_date(appt.scheduledStart)} · '
                    '${_time(appt.scheduledStart)} – ${_time(appt.scheduledEnd)}'
                    '${appt.type == 'FollowUp' ? ' · Follow-up' : ''}',
                    style:
                        const TextStyle(fontSize: 12, color: AppColors.muted),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          if (appt.status != 'Cancelled') ...[
            const FadeSlideIn(
              delay: Duration(milliseconds: 80),
              distance: 8,
              child: SectionHeader('Progress'),
            ),
            const SizedBox(height: 8),
            FadeSlideIn(
              delay: const Duration(milliseconds: 120),
              distance: 10,
              child: AppCard(
                child: StatusTimeline(
                  steps: const [
                    'Request Submitted',
                    'Clinic Selected',
                    'Vet Assigned',
                    'Appointment Upcoming',
                    'Examination',
                    'Completed',
                  ],
                  currentIndex: appt.status == 'Completed' ? 6 : 3,
                ),
              ),
            ),
            const SizedBox(height: 16),
          ],
          const FadeSlideIn(
            delay: Duration(milliseconds: 160),
            distance: 8,
            child: SectionHeader('Details'),
          ),
          const SizedBox(height: 8),
          FadeSlideIn(
            delay: const Duration(milliseconds: 200),
            distance: 10,
            child: AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  DetailRow('Pet', value: appt.petName ?? appt.petId),
                  if (appt.type == 'FollowUp')
                    const DetailRow('Type', value: 'Follow-up'),
                  DetailRow('Date', value: _date(appt.scheduledStart)),
                  DetailRow('Time',
                      value:
                          '${_time(appt.scheduledStart)} – ${_time(appt.scheduledEnd)}'),
                  DetailRow('Veterinarian',
                      value: appt.veterinarianName ?? appt.veterinarianId),
                  if (_clinic != null)
                    DetailRow('Clinic', value: _clinic!.name),
                  if (_clinic != null)
                    DetailRow('Clinic address', value: _clinic!.addressLabel),
                  DetailRow('Status', child: StatusBadge(appt.status)),
                  if (appt.symptoms != null && appt.symptoms!.isNotEmpty)
                    DetailRow('Symptoms', value: appt.symptoms!),
                  if (appt.notes != null && appt.notes!.isNotEmpty)
                    DetailRow('Notes', value: appt.notes!),
                ],
              ),
            ),
          ),
          const SizedBox(height: 16),
          if (_clinicResolved && _clinic != null)
            FadeSlideIn(
              delay: const Duration(milliseconds: 260),
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
