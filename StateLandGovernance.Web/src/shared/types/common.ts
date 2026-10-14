export interface ApiErrorResponse {
  status: number;
  title: string;
  detail: string;
  errors?: string[] | null;
}
