export interface ActionInfo {
  type: string;
  displayName: string;
}

export interface ProfileSummary {
  id: string;
  name: string;
}

export type VariableSnapshot = Record<string, unknown>;

export interface VariableInfo {
  name: string;
  description: string;
  example: string;
  category: string;
}
