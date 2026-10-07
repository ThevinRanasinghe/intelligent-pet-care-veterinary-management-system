/** @vitest-environment jsdom */
import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor, fireEvent, cleanup } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach, afterEach } from 'vitest';
import { NewConsultationModal } from '../../features/consultations/NewConsultationModal';
import { petService, consultationService, Pet, ConsultationRequestApi, NearestClinicApi } from '../../services/api';

// Mock the API services
vi.mock('../../services/api', async () => {
  const actual = await vi.importActual('../../services/api');
  return {
    ...actual,
    petService: {
      getAllPets: vi.fn(),
    },
    consultationService: {
      getNearestClinic: vi.fn(),
      validateOwnership: vi.fn(),
      createConsultation: vi.fn(),
    },
  };
});

describe('NewConsultationModal Component Tests', () => {
  const mockPets: Pet[] = [
    {
      id: 'pet-001',
      ownerId: 'owner-123',
      name: 'Max',
      species: 'Dog',
      breed: 'Golden Retriever',
    },
    {
      id: 'pet-002',
      ownerId: 'owner-123',
      name: 'Luna',
      species: 'Cat',
      breed: 'Persian',
    },
  ];

  const defaultProps = {
    isOpen: true,
    onClose: vi.fn(),
    onSubmit: vi.fn(),
  };

  beforeEach(() => {
    vi.clearAllMocks();
    (petService.getAllPets as any).mockResolvedValue(mockPets);
    (consultationService.validateOwnership as any).mockResolvedValue({
      isValid: true,
      message: 'Pet belongs to the owner.',
    });
    (consultationService.createConsultation as any).mockResolvedValue({
      id: 'CON-12345678',
      petId: 'pet-001',
      ownerId: 'owner-123',
      symptoms: 'Persistent coughing',
      urgency: 'Medium',
      status: 'Draft',
      createdAt: new Date().toISOString(),
      updatedAt: new Date().toISOString(),
    } as ConsultationRequestApi);
  });

  afterEach(() => {
    cleanup();
    document.body.innerHTML = '';
    vi.restoreAllMocks();
  });

  // ==========================================================================
  // TC-CONS-WEB-01: Consultation form renders correctly
  // ==========================================================================
  it('TC-CONS-WEB-01: Consultation form renders correctly', async () => {
    // Arrange & Act
    render(<NewConsultationModal {...defaultProps} />);

    // Assert
    expect(screen.getByRole('dialog')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: /new consultation/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/registered pet/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/symptoms/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/urgency/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/preferred date/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/preferred time/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/budget limit/i)).toBeInTheDocument();
    expect(screen.getByLabelText(/preferred clinic/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /use my location/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/additional notes/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /create consultation/i })).toBeInTheDocument();
  });

  // ==========================================================================
  // TC-CONS-WEB-02: Pet selection is required
  // ==========================================================================
  it('TC-CONS-WEB-02: Pet selection is required', async () => {
    // Arrange
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Click submit without selecting pet
    const submitBtn = screen.getByRole('button', { name: /create consultation/i });
    fireEvent.click(submitBtn);

    // Assert
    expect(await screen.findByText(/please select a registered pet\./i)).toBeInTheDocument();
    expect(consultationService.createConsultation).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-03: Symptoms are required
  // ==========================================================================
  it('TC-CONS-WEB-03: Symptoms are required', async () => {
    // Arrange
    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Select pet, leave symptoms empty, click submit
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    const submitBtn = screen.getByRole('button', { name: /create consultation/i });
    await user.click(submitBtn);

    // Assert
    expect(await screen.findByText(/please describe the pet's symptoms\./i)).toBeInTheDocument();
    expect(consultationService.createConsultation).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-04: Preferred date is required
  // ==========================================================================
  it('TC-CONS-WEB-04: Preferred date is required', async () => {
    // Arrange
    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Fill pet and symptoms, leave date empty
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    await user.type(screen.getByLabelText(/symptoms/i), 'Vomiting and lethargy');
    const submitBtn = screen.getByRole('button', { name: /create consultation/i });
    await user.click(submitBtn);

    // Assert
    expect(await screen.findByText(/please select a preferred date\./i)).toBeInTheDocument();
    expect(consultationService.createConsultation).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-05: Preferred time is required
  // ==========================================================================
  it('TC-CONS-WEB-05: Preferred time is required', async () => {
    // Arrange
    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Fill pet, symptoms, date, leave time empty
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    await user.type(screen.getByLabelText(/symptoms/i), 'Vomiting and lethargy');
    fireEvent.change(screen.getByLabelText(/preferred date/i), { target: { value: '2026-10-15' } });

    const submitBtn = screen.getByRole('button', { name: /create consultation/i });
    await user.click(submitBtn);

    // Assert
    expect(await screen.findByText(/please select a preferred time\./i)).toBeInTheDocument();
    expect(consultationService.createConsultation).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-06: Invalid/negative budget is rejected
  // ==========================================================================
  it('TC-CONS-WEB-06: Invalid/negative budget is rejected', async () => {
    // Arrange
    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Fill required fields and provide negative budget
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    await user.type(screen.getByLabelText(/symptoms/i), 'Skin irritation');
    fireEvent.change(screen.getByLabelText(/preferred date/i), { target: { value: '2026-10-15' } });
    fireEvent.change(screen.getByLabelText(/preferred time/i), { target: { value: '10:30' } });
    fireEvent.change(screen.getByLabelText(/budget limit/i), { target: { value: '-50' } });

    const form = screen.getByRole('dialog').querySelector('form')!;
    fireEvent.submit(form);

    // Assert
    expect(await screen.findByText(/budget must be a valid positive amount\./i)).toBeInTheDocument();
    expect(consultationService.createConsultation).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-07: Pet ownership validation failure is handled
  // ==========================================================================
  it('TC-CONS-WEB-07: Pet ownership validation failure is handled', async () => {
    // Arrange
    (consultationService.validateOwnership as any).mockResolvedValue({
      isValid: false,
      message: 'The selected pet does not belong to the authenticated owner.',
    });

    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Fill form and submit
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    await user.type(screen.getByLabelText(/symptoms/i), 'Limping on rear leg');
    fireEvent.change(screen.getByLabelText(/preferred date/i), { target: { value: '2026-10-15' } });
    fireEvent.change(screen.getByLabelText(/preferred time/i), { target: { value: '14:00' } });

    const submitBtn = screen.getByRole('button', { name: /create consultation/i });
    await user.click(submitBtn);

    // Assert
    expect(await screen.findByText(/the selected pet does not belong to the authenticated owner\./i)).toBeInTheDocument();
    expect(consultationService.createConsultation).not.toHaveBeenCalled();
    expect(defaultProps.onSubmit).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-08: Valid consultation request is successfully created
  // ==========================================================================
  it('TC-CONS-WEB-08: Valid consultation request is successfully created', async () => {
    // Arrange
    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Fill complete valid form and submit
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    await user.type(screen.getByLabelText(/symptoms/i), 'Fever and ear infection');
    await user.selectOptions(screen.getByLabelText(/urgency/i), 'High');
    fireEvent.change(screen.getByLabelText(/preferred date/i), { target: { value: '2026-10-20' } });
    fireEvent.change(screen.getByLabelText(/preferred time/i), { target: { value: '09:00' } });
    fireEvent.change(screen.getByLabelText(/budget limit/i), { target: { value: '15000' } });
    await user.type(screen.getByLabelText(/additional notes/i), 'Prefers Dr. Smith');

    const submitBtn = screen.getByRole('button', { name: /create consultation/i });
    await user.click(submitBtn);

    // Assert
    await waitFor(() => {
      expect(consultationService.validateOwnership).toHaveBeenCalledWith('pet-001', 'owner-123');
      expect(consultationService.createConsultation).toHaveBeenCalled();
      expect(defaultProps.onSubmit).toHaveBeenCalled();
      expect(defaultProps.onClose).toHaveBeenCalled();
    });
  });

  // ==========================================================================
  // TC-CONS-WEB-09: User location can be captured using browser geolocation
  // ==========================================================================
  it('TC-CONS-WEB-09: User location can be captured using browser geolocation', async () => {
    // Arrange: Mock navigator.geolocation
    const mockGeolocation = {
      getCurrentPosition: vi.fn().mockImplementation((success) => {
        success({
          coords: {
            latitude: 6.9271,
            longitude: 79.8612,
          },
        });
      }),
    };
    Object.defineProperty(global.navigator, 'geolocation', {
      value: mockGeolocation,
      configurable: true,
    });

    (consultationService.getNearestClinic as any).mockResolvedValue({
      name: 'City Pet Care Central',
      address: '123 Galle Road, Colombo',
      latitude: 6.93,
      longitude: 79.86,
      distanceKm: 1.2,
    } as NearestClinicApi);

    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);

    // Act: Click "Use My Location"
    const locationBtn = screen.getByRole('button', { name: /use my location/i });
    await user.click(locationBtn);

    // Assert
    expect(mockGeolocation.getCurrentPosition).toHaveBeenCalled();
    expect(await screen.findByText(/✓ location captured/i)).toBeInTheDocument();
  });

  // ==========================================================================
  // TC-CONS-WEB-10: Nearest clinic is retrieved after location capture
  // ==========================================================================
  it('TC-CONS-WEB-10: Nearest clinic is retrieved after location capture', async () => {
    // Arrange
    const mockGeolocation = {
      getCurrentPosition: vi.fn().mockImplementation((success) => {
        success({
          coords: {
            latitude: 6.9271,
            longitude: 79.8612,
          },
        });
      }),
    };
    Object.defineProperty(global.navigator, 'geolocation', {
      value: mockGeolocation,
      configurable: true,
    });

    (consultationService.getNearestClinic as any).mockResolvedValue({
      name: 'Downtown Veterinary Hospital',
      address: '45 Lotus Rd, Colombo',
      latitude: 6.935,
      longitude: 79.855,
      distanceKm: 2.5,
    } as NearestClinicApi);

    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);

    // Act: Click "Use My Location"
    await user.click(screen.getByRole('button', { name: /use my location/i }));

    // Assert
    await waitFor(() => {
      expect(consultationService.getNearestClinic).toHaveBeenCalledWith(6.9271, 79.8612);
    });
  });

  // ==========================================================================
  // TC-CONS-WEB-11: Nearest clinic information is auto-filled
  // ==========================================================================
  it('TC-CONS-WEB-11: Nearest clinic information is auto-filled', async () => {
    // Arrange
    const mockGeolocation = {
      getCurrentPosition: vi.fn().mockImplementation((success) => {
        success({
          coords: {
            latitude: 6.9271,
            longitude: 79.8612,
          },
        });
      }),
    };
    Object.defineProperty(global.navigator, 'geolocation', {
      value: mockGeolocation,
      configurable: true,
    });

    const clinicInfo: NearestClinicApi = {
      name: 'Colombo Central Vet Clinic',
      address: '100 Havelock Rd, Colombo 05',
      latitude: 6.89,
      longitude: 79.86,
      distanceKm: 0.8,
    };
    (consultationService.getNearestClinic as any).mockResolvedValue(clinicInfo);

    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);

    // Act
    await user.click(screen.getByRole('button', { name: /use my location/i }));

    // Assert
    expect(await screen.findByText('Colombo Central Vet Clinic')).toBeInTheDocument();
    expect(screen.getByText('100 Havelock Rd, Colombo 05')).toBeInTheDocument();
    expect(screen.getByText('0.8 km away')).toBeInTheDocument();
    expect(screen.getByLabelText(/preferred clinic/i)).toHaveValue('Colombo Central Vet Clinic');
  });

  // ==========================================================================
  // TC-CONS-WEB-12: Geolocation failure displays an appropriate error
  // ==========================================================================
  it('TC-CONS-WEB-12: Geolocation failure displays an appropriate error', async () => {
    // Arrange: Simulate user denying geolocation permission
    const mockGeolocation = {
      getCurrentPosition: vi.fn().mockImplementation((_success, error) => {
        error({ code: 1, message: 'User denied Geolocation' });
      }),
    };
    Object.defineProperty(global.navigator, 'geolocation', {
      value: mockGeolocation,
      configurable: true,
    });

    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);

    // Act
    await user.click(screen.getByRole('button', { name: /use my location/i }));

    // Assert
    expect(await screen.findByText(/unable to get your location\. please allow location access\./i)).toBeInTheDocument();
    expect(consultationService.getNearestClinic).not.toHaveBeenCalled();
  });

  // ==========================================================================
  // TC-CONS-WEB-13: Consultation request payload is correctly submitted
  // ==========================================================================
  it('TC-CONS-WEB-13: Consultation request payload is correctly submitted', async () => {
    // Arrange
    const mockGeolocation = {
      getCurrentPosition: vi.fn().mockImplementation((success) => {
        success({
          coords: { latitude: 6.9271, longitude: 79.8612 },
        });
      }),
    };
    Object.defineProperty(global.navigator, 'geolocation', {
      value: mockGeolocation,
      configurable: true,
    });

    (consultationService.getNearestClinic as any).mockResolvedValue({
      name: 'Kandy Road Vet Clinic',
      address: '200 Kandy Rd',
      latitude: 6.95,
      longitude: 79.88,
      distanceKm: 3.1,
    });

    const user = userEvent.setup();
    render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /luna — cat — persian/i })).toBeInTheDocument();

    // Act: Capture location, fill form and submit
    await user.click(screen.getByRole('button', { name: /use my location/i }));
    await screen.findByText(/✓ location captured/i);

    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-002');
    await user.type(screen.getByLabelText(/symptoms/i), 'Loss of appetite and sneezing');
    await user.selectOptions(screen.getByLabelText(/urgency/i), 'Emergency');
    fireEvent.change(screen.getByLabelText(/preferred date/i), { target: { value: '2026-11-01' } });
    fireEvent.change(screen.getByLabelText(/preferred time/i), { target: { value: '11:15' } });
    fireEvent.change(screen.getByLabelText(/budget limit/i), { target: { value: '25000' } });
    await user.type(screen.getByLabelText(/additional notes/i), 'Needs urgent care');

    await user.click(screen.getByRole('button', { name: /create consultation/i }));

    // Assert: Verify exact payload structure sent to API
    await waitFor(() => {
      expect(consultationService.createConsultation).toHaveBeenCalledWith({
        petId: 'pet-002',
        ownerId: 'owner-123',
        symptoms: 'Loss of appetite and sneezing',
        urgency: 'Emergency',
        preferredDate: new Date('2026-11-01T11:15:00').toISOString(),
        preferredTime: '11:15:00',
        budget: 25000,
        symptomPhotoUrl: null,
        latitude: 6.9271,
        longitude: 79.8612,
        additionalNotes: 'Needs urgent care',
      });
    });
  });

  // ==========================================================================
  // TC-CONS-WEB-14: Form resets after successful submission
  // ==========================================================================
  it('TC-CONS-WEB-14: Form resets after successful submission', async () => {
    // Arrange
    const user = userEvent.setup();
    const { rerender } = render(<NewConsultationModal {...defaultProps} />);
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();

    // Act: Fill and submit
    await user.selectOptions(screen.getByLabelText(/registered pet/i), 'pet-001');
    await user.type(screen.getByLabelText(/symptoms/i), 'Coughing');
    fireEvent.change(screen.getByLabelText(/preferred date/i), { target: { value: '2026-10-25' } });
    fireEvent.change(screen.getByLabelText(/preferred time/i), { target: { value: '15:00' } });

    await user.click(screen.getByRole('button', { name: /create consultation/i }));

    await waitFor(() => {
      expect(defaultProps.onSubmit).toHaveBeenCalled();
      expect(defaultProps.onClose).toHaveBeenCalled();
    });

    // Reopen modal to verify reset
    rerender(<NewConsultationModal {...defaultProps} isOpen={true} />);

    expect(screen.getByLabelText(/registered pet/i)).toHaveValue('');
    expect(screen.getByLabelText(/symptoms/i)).toHaveValue('');
    expect(screen.getByLabelText(/urgency/i)).toHaveValue('Medium');
    expect(screen.getByLabelText(/preferred date/i)).toHaveValue('');
    expect(screen.getByLabelText(/preferred time/i)).toHaveValue('');
    expect(screen.getByLabelText(/budget limit/i)).toHaveValue(null);
  });

  // ==========================================================================
  // TC-CONS-WEB-15: Registered pets are loaded for the owner
  // ==========================================================================
  it('TC-CONS-WEB-15: Registered pets are loaded for the owner', async () => {
    // Arrange & Act
    render(<NewConsultationModal {...defaultProps} />);

    // Assert
    await waitFor(() => {
      expect(petService.getAllPets).toHaveBeenCalledTimes(1);
    });

    const select = screen.getByLabelText(/registered pet/i) as HTMLSelectElement;
    expect(select.options.length).toBe(3); // default placeholder + 2 pets
    expect(await screen.findByRole('option', { name: /max — dog — golden retriever/i })).toBeInTheDocument();
    expect(screen.getByRole('option', { name: /luna — cat — persian/i })).toBeInTheDocument();
  });
});
