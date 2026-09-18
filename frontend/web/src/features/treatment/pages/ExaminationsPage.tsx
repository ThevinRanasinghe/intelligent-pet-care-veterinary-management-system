import React, { useEffect, useState } from 'react';
import { treatmentApi, Examination } from '../api/treatmentApi';

export const ExaminationsPage: React.FC = () => {
    const [examinations, setExaminations] = useState<Examination[]>([]);
    const [loading, setLoading] = useState<boolean>(true);
    const [formData, setFormData] = useState<Examination>({
        petId: '',
        vetId: '',
        examinationDate: new Date().toISOString().split('T')[0],
        symptoms: '',
        notes: '',
    });

    useEffect(() => {
        loadExaminations();
    }, []);

    const loadExaminations = async () => {
        try {
            const response = await treatmentApi.getExaminations();
            setExaminations(response.data);
        } catch (err) {
            console.error('Failed to load examinations', err);
        } finally {
            setLoading(false);
        }
    };

    const handleSubmit = async (e: React.FormEvent) => {
        e.preventDefault();
        try {
            await treatmentApi.createExamination(formData);
            loadExaminations(); // Refresh list after creation
            setFormData({ petId: '', vetId: '', examinationDate: '', symptoms: '', notes: '' });
        } catch (err) {
            console.error('Failed to submit examination', err);
        }
    };

    if (loading) return <div>Loading examinations...</div>;

    return (
        <div style={{ padding: '20px' }}>
            <h2>Veterinary Examinations</h2>

            {/* Examination Entry Form */}
            <form onSubmit={handleSubmit} style={{ marginBottom: '30px', display: 'flex', flexDirection: 'column', gap: '10px', maxWidth: '400px' }}>
                <h3>New Examination</h3>
                <input
                    type="text"
                    placeholder="Pet ID"
                    value={formData.petId}
                    onChange={(e) => setFormData({ ...formData, petId: e.target.value })}
                    required
                />
                <input
                    type="text"
                    placeholder="Vet ID"
                    value={formData.vetId}
                    onChange={(e) => setFormData({ ...formData, vetId: e.target.value })}
                    required
                />
                <textarea
                    placeholder="Observed Symptoms"
                    value={formData.symptoms}
                    onChange={(e) => setFormData({ ...formData, symptoms: e.target.value })}
                    required
                />
                <button type="submit">Save Examination</button>
            </form>

            {/* Examination Data Table */}
            <table border={1} cellPadding={8} style={{ borderCollapse: 'collapse', width: '100%' }}>
                <thead>
                    <tr>
                        <th>Date</th>
                        <th>Pet ID</th>
                        <th>Vet ID</th>
                        <th>Symptoms</th>
                    </tr>
                </thead>
                <tbody>
                    {examinations.map((exam) => (
                        <tr key={exam.id}>
                            <td>{exam.examinationDate}</td>
                            <td>{exam.petId}</td>
                            <td>{exam.vetId}</td>
                            <td>{exam.symptoms}</td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );
};
