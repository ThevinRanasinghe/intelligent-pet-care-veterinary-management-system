import { useState } from 'react';
import { Package, AlertTriangle, Clock, Truck } from 'lucide-react';
import DashboardLayout from '../../shared/DashboardLayout';
import InventoryManagementView from './InventoryManagementView';

export default function InventoryDashboard() {
  const [activeTab, setActiveTab] = useState('catalog');

  const navItems = [
    {
      label: 'Stock Overview',
      icon: <Package size={18} />,
      active: activeTab === 'catalog',
      onClick: () => setActiveTab('catalog'),
    },
    {
      label: 'Low Stock Alerts',
      icon: <AlertTriangle size={18} />,
      active: activeTab === 'lowStock',
      onClick: () => setActiveTab('lowStock'),
    },
    {
      label: 'Expiring Batches',
      icon: <Clock size={18} />,
      active: activeTab === 'expiring',
      onClick: () => setActiveTab('expiring'),
    },
    {
      label: 'Suppliers',
      icon: <Truck size={18} />,
      active: activeTab === 'suppliers',
      onClick: () => setActiveTab('suppliers'),
    },
  ];

  return (
    <DashboardLayout
      pageTitle="Medicine & Inventory Portal"
      pageSubtitle="Track medical supplies, medications, FEFO batch expirations, and stock consignments"
      navItems={navItems}
    >
      <InventoryManagementView defaultTab={activeTab} key={activeTab} />
    </DashboardLayout>
  );
}
