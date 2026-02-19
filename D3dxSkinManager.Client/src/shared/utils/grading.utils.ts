import { GradingLevel } from '../../shared/types/mod.types';

export const getGradingColor = (grading: string): string => {
  switch (grading) {
    case 'G': return 'green';
    case 'P': return 'blue';
    case 'R': return 'orange';
    case 'X': return 'red';
    default: return 'default';
  }
};

export const getGradingLabel = (grading: GradingLevel): string => {
  switch (grading) {
    case 'G': return 'G - General';
    case 'P': return 'P - Parental Guidance';
    case 'R': return 'R - Restricted';
    case 'X': return 'X - Extreme';
  }
};

export const gradingOptions = [
  { value: 'G', label: 'G - General' },
  { value: 'P', label: 'P - Parental' },
  { value: 'R', label: 'R - Restricted' },
  { value: 'X', label: 'X - Extreme' },
];
