// Extracted so App can read the first-run flag WITHOUT importing the (lazy-loaded) OnboardingWizard
// component — keeps the wizard out of the initial bundle.
export const ONBOARDING_DONE_KEY = 'd3dx.onboarding.completed.v1';
