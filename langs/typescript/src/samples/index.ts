import sas from './sas.json';
import eds from './eds.json';

export const SAMPLES = [
  { id: 'sas', label: 'SAS — Service-Oriented Architecture', raw: sas },
  { id: 'eds', label: 'EDS — Enterprise Decision Space', raw: eds },
] as const;
