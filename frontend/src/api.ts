export type Mastery = 'nueva' | 'en_progreso' | 'con_errores' | 'dominada'

export interface OptionPublic { key: string; text: string }
export interface Progress { attempts: number; correctAttempts: number; lastCorrect: boolean | null; mastery: Mastery; lastAttemptAt: string | null }
export interface QuestionPublic {
  id: number; originalId: number; courseId: number; moduleId: number; moduleName: string; text: string; type: 'unica' | 'multiple'
  options: OptionPublic[]; needsReview: boolean; reviewNotes: string[]; isFavorite: boolean; isMarkedForReview: boolean; progress: Progress
}
export interface OptionFeedback { key: string; text: string; isCorrect: boolean; selected: boolean }
export interface Feedback {
  isCorrect: boolean; selected: string[]; correctAnswers: string[]; options: OptionFeedback[]
  explanation: string; memoryRule: string; originalState: string; source: string | null; needsReview: boolean; reviewNotes: string[]
}
export interface CheckResult { feedback: Feedback; progress: Progress }

export interface CourseStats {
  id: number; title: string; moduleCount: number; total: number; inReview: number; attempted: number; correctLast: number
  mastered: number; withErrors: number; progressPercentage: number; bankVersions: number
}
export interface ModuleStats {
  id: number; courseId: number; name: string; sortOrder: number; total: number; inReview: number; attempted: number
  correctLast: number; mastered: number; withErrors: number; progressPercentage: number
}
export interface Activity { at: string; context: string; questionId: number; originalId: number; module: string; text: string; isCorrect: boolean }
export interface ExamSummary {
  id: number; courseId: number; title: string; mode: string; status: string; createdAt: string; finishedAt: string | null
  total: number; correct: number; percentage: number; passed: boolean
}
export interface Dashboard {
  courses: CourseStats[]; totalAttempts: number; correctAttempts: number; accuracy: number; recentActivity: Activity[]
  recentExams: ExamSummary[]; continueStudy: { courseId: number; moduleId: number; moduleName: string } | null; passThreshold: number
}
export interface RuleGroup { moduleId: number; module: string; rules: { questionId: number; originalId: number; rule: string; needsReview: boolean }[] }
export interface ErrorEntry {
  questionId: number; originalId: number; courseId: number; moduleId: number; module: string; text: string; attempts: number; errors: number
  lastSelected: string[]; correctAnswers: string[]; lastAttemptAt: string; lastErrorAt: string; mastery: Mastery; memoryRule: string; needsReview: boolean
}
export interface ErrorLog { entries: ErrorEntry[]; byModule: { moduleId: number; module: string; questions: number; errors: number }[] }

export type ExamFilter = 'todas' | 'nuevas' | 'errores' | 'favoritas' | 'repaso'
export interface ExamConfig {
  courseId: number; moduleIds?: number[] | null; count?: number | null; order: 'aleatorio' | 'secuencial'; shuffleOptions: boolean
  filter: ExamFilter; includeReview: boolean; timeLimitMinutes?: number | null; mode: 'practica' | 'examen'
}
export interface Eligibility { eligible: number; excludedForReview: number; max: number }
export interface ExamQuestion {
  position: number; questionId: number; originalId: number; moduleName: string; text: string; type: 'unica' | 'multiple'
  options: OptionPublic[]; selected: string[]; marked: boolean; checked: boolean; needsReview: boolean; reviewNotes: string[]; feedback: Feedback | null
}
export interface ExamResult {
  total: number; correct: number; incorrect: number; unanswered: number; percentage: number; passThreshold: number; passed: boolean
  durationSeconds: number | null; byModule: { moduleId: number; module: string; total: number; correct: number; percentage: number }[]
}
export interface Exam {
  id: number; courseId: number; courseTitle: string; title: string; mode: 'practica' | 'examen'; status: 'en_curso' | 'finalizado'
  createdAt: string; deadline: string | null; timeLimitSeconds: number | null; finishedAt: string | null; total: number; answered: number
  config: ExamConfig; questions: ExamQuestion[]; result: ExamResult | null
}
export interface ImportIssue { questionId: number | null; severity: 'error' | 'advertencia' | string; code: string; message: string }
export interface ImportReport {
  status: string; fileName: string; title: string | null; declaredVersion: string | null; declaredTotal: number | null; note: string | null
  totalFound: number; moduleCount: number; single: number; multiple: number; reasoned: number; needsReview: number; flaggedByValidation: number
  modules: { name: string; total: number; inReview: number }[]; issues: ImportIssue[]; canCommit: boolean; alreadyImported: boolean
  added: number; updated: number; unchanged: number; missingFromFile: number; courseId: number | null; bankVersionId: number | null
}
export interface BankVersion {
  id: number; courseId: number; courseTitle: string; versionNumber: number; declaredVersion: string; declaredTotal: number | null
  sourceFileName: string; fileHash: string; importedAt: string; report: ImportReport | null
}
export interface AdminQuestion {
  id: number; originalId: number; courseId: number; courseTitle: string; moduleId: number; module: string; text: string; type: string
  originalState: string; hasValidationIssues: boolean; needsReview: boolean; currentVersion: number
}
export interface AdminQuestionDetail {
  summary: AdminQuestion; options: { key: string; text: string; isCorrect: boolean }[]; correctAnswers: string[]; explanation: string
  memoryRule: string; source: string | null; reviewNotes: string[]; versions: { versionNumber: number; bankVersionId: number; createdAt: string; contentHash: string }[]
}
export interface Paged<T> { items: T[]; total: number; page: number; pageSize: number }
export interface Profile { id: number; name: string; passThreshold: number }

const PROFILE_KEY = 'studyhub.profileId'
export const getProfileId = () => localStorage.getItem(PROFILE_KEY)
export const setProfileId = (id: number) => localStorage.setItem(PROFILE_KEY, String(id))

export class ApiError extends Error {
  status: number
  body: unknown
  constructor(status: number, message: string, body: unknown) { super(message); this.status = status; this.body = body }
}

async function request<T>(method: string, url: string, body?: unknown): Promise<T> {
  const headers: Record<string, string> = {}
  const profile = getProfileId()
  if (profile) headers['X-Profile-Id'] = profile
  let payload: BodyInit | undefined
  if (body instanceof FormData) payload = body
  else if (body !== undefined) { headers['Content-Type'] = 'application/json'; payload = JSON.stringify(body) }
  const res = await fetch(url, { method, headers, body: payload })
  const text = await res.text()
  const data = text ? JSON.parse(text) : undefined
  if (!res.ok) {
    if (res.status === 400 && profile && data?.detail === 'El perfil indicado no existe.') localStorage.removeItem(PROFILE_KEY)
    throw new ApiError(res.status, data?.detail ?? data?.title ?? `Error ${res.status}`, data)
  }
  return data as T
}

const upload = (file: File) => { const f = new FormData(); f.append('file', file); return f }
const qs = (p: Record<string, string | number | undefined | null>) => {
  const s = new URLSearchParams()
  Object.entries(p).forEach(([k, v]) => { if (v !== undefined && v !== null && v !== '') s.set(k, String(v)) })
  const r = s.toString()
  return r ? `?${r}` : ''
}

export const api = {
  dashboard: () => request<Dashboard>('GET', '/api/dashboard'),
  courses: () => request<CourseStats[]>('GET', '/api/courses'),
  modules: (courseId: number) => request<ModuleStats[]>('GET', `/api/courses/${courseId}/modules`),
  rules: (p: { courseId?: number; moduleId?: number; search?: string }) => request<RuleGroup[]>('GET', `/api/rules${qs(p)}`),
  errors: (courseId?: number) => request<ErrorLog>('GET', `/api/errors${qs({ courseId })}`),
  marked: (kind: 'favorito' | 'repasar', courseId?: number) => request<QuestionPublic[]>('GET', `/api/marks/${kind}${qs({ courseId })}`),
  setMark: (kind: 'favorito' | 'repasar', questionId: number, on: boolean) =>
    request<void>(on ? 'PUT' : 'DELETE', `/api/marks/${kind}/${questionId}`),
  moduleQuestions: (moduleId: number) => request<QuestionPublic[]>('GET', `/api/study/modules/${moduleId}/questions`),
  question: (id: number) => request<QuestionPublic>('GET', `/api/study/questions/${id}`),
  check: (questionId: number, selected: string[]) => request<CheckResult>('POST', '/api/study/check', { questionId, selected }),
  exams: () => request<ExamSummary[]>('GET', '/api/exams'),
  eligible: (c: ExamConfig) => request<Eligibility>('POST', '/api/exams/eligible', c),
  createExam: (c: ExamConfig) => request<Exam>('POST', '/api/exams', c),
  exam: (id: number) => request<Exam>('GET', `/api/exams/${id}`),
  answer: (id: number, position: number, body: { selected?: string[]; marked?: boolean }) =>
    request<Exam>('PUT', `/api/exams/${id}/questions/${position}`, body),
  checkExam: (id: number, position: number) => request<Exam>('POST', `/api/exams/${id}/questions/${position}/check`),
  finish: (id: number) => request<Exam>('POST', `/api/exams/${id}/finish`),
  retryErrors: (id: number) => request<Exam>('POST', `/api/exams/${id}/retry-errors`),
  preview: (file: File) => request<ImportReport>('POST', '/api/import/preview', upload(file)),
  commit: (file: File) => request<ImportReport>('POST', '/api/import/commit', upload(file)),
  banks: () => request<BankVersion[]>('GET', '/api/admin/banks'),
  adminQuestions: (p: { courseId?: number; moduleId?: number; type?: string; state?: string; search?: string; page?: number; pageSize?: number }) =>
    request<Paged<AdminQuestion>>('GET', `/api/admin/questions${qs(p)}`),
  adminQuestion: (id: number) => request<AdminQuestionDetail>('GET', `/api/admin/questions/${id}`),
  profiles: () => request<Profile[]>('GET', '/api/profiles'),
  createProfile: (name: string, passThreshold?: number) => request<Profile>('POST', '/api/profiles', { name, passThreshold }),
  updateProfile: (id: number, body: { name?: string; passThreshold?: number }) => request<Profile>('PUT', `/api/profiles/${id}`, body),
}
