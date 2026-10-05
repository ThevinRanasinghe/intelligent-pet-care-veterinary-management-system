import { useEffect, useState } from 'react';
import { getExaminations, createExamination } from '../../../services/treatmentService';
import type { Examination } from '../../../types/domain';

export const ExaminationsPage = () => {
    const [examinations, setExaminations] = useState<Examination[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [formData, setFormData] = useState({
        petId: '',
        veterinarianId: '',
        consultationRequestId: '',
        symptoms: '',
        notes: '',
        examinationDate: new Date().toISOString().split('T')[0],
    });

    useEffect(() => {
        loadExaminations();
    }, []);

    const loadExaminations = async () => {
        try {
            const data = await getExaminations();
            setExaminations(data);
        } catch (err) {
            console.error('Failed to load examinations', err);
        } finally {
            setLoading(false);
        }
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await createExamination({
                petId: formData.petId,
                veterinarianId: formData.veterinarianId,
                consultationRequestId: formData.consultationRequestId || null,
                symptoms: formData.symptoms,
                notes: formData.notes,
                examinationDate: formData.examinationDate,
            });
            loadExaminations();
            setFormData({ petId: '', veterinarianId: '', consultationRequestId: '', symptoms: '', notes: '', examinationDate: new Date().toISOString().split('T')[0] });
        } catch (err) {
            console.error('Failed to submit examination', err);
        }
    };

    if (loading) return <p className="muted">Loading examinations...</p>;

    return (
        <div className="page-wrap">
            <div className="page-heading">
                <h2>Examinations</h2>
                <p className="eyebrow">Create and view clinical examinations</p>
            </div>

            <form onSubmit={handleSubmit} className="form-grid">
                <label>
                    Pet ID
                    <input value={formData.petId} onChange={(e) => setFormData({ ...formData, petId: e.target.value })} required />
                </label>
                <label>
                    Veterinarian ID
                    <input value={formData.veterinarianId} onChange={(e) => setFormData({ ...formData, veterinarianId: e.target.value })} required />
                </label>
                <label>
                    Symptoms
                    <input value={formData.symptoms} onChange={(e) => setFormData({ ...formData, symptoms: e.target.value })} required />
                </label>
                <label>
                    Notes
                    <input value={formData.notes} onChange={(e) => setFormData({ ...formData, notes: e.target.value })} />
                </label>
                <label>
                    Examination Date
                    <input type="date" value={formData.examinationDate} onChange={(e) => setFormData({ ...formData, examinationDate: e.target.value })} required />
                </label>
                <button type="submit" className="btn">Create Examination</button>
            </form>

            <div className="table-wrap">
                <table>
                    <thead>
                        <tr>
                            <th>ID</th>
                            <th>Pet ID</th>
                            <th>Vet ID</th>
                            <th>Symptoms</th>
                            <th>Date</th>
                        </tr>
                    </thead>
                    <tbody>
                        {examinations.map((exam) => (
                            <tr key={exam.id}>
                                <td>{exam.id}</td>
                                <td>{exam.petId}</td>
                                <td>{exam.veterinarianId}</td>
                                <td>{exam.symptoms}</td>
                                <td>{exam.examinationDate}</td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
};
