import { ClipboardList, Info } from 'lucide-react'
import { useEffect, useState } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { api, type Eligibility, type ExamConfig, type ExamFilter, type ModuleStats } from '../api'
import { Empty, ErrorState, Loading, PageHeader, useAsync } from '../components/ui'

const filters: { value: ExamFilter; label: string; hint: string }[] = [
  { value: 'todas', label: 'Todas', hint: 'Cualquier pregunta de los módulos elegidos' },
  { value: 'nuevas', label: 'Nuevas', hint: 'Nunca respondidas' },
  { value: 'errores', label: 'Errores', hint: 'Último intento incorrecto' },
  { value: 'favoritas', label: 'Favoritas', hint: 'Marcadas como favoritas' },
  { value: 'repaso', label: 'Para repasar', hint: 'Marcadas para repasar' },
]

export function ExamConfigPage() {
  const [params] = useSearchParams()
  const navigate = useNavigate()
  const courses = useAsync(api.courses, [])
  const [courseId, setCourseId] = useState<number | null>(params.get('courseId') ? Number(params.get('courseId')) : null)
  const [modules, setModules] = useState<ModuleStats[]>([])
  const [moduleIds, setModuleIds] = useState<number[]>(params.get('moduleId') ? [Number(params.get('moduleId'))] : [])
  const [count, setCount] = useState('10')
  const [mode, setMode] = useState<ExamConfig['mode']>('examen')
  const [order, setOrder] = useState<ExamConfig['order']>('aleatorio')
  const [shuffleOptions, setShuffleOptions] = useState(false)
  const [filter, setFilter] = useState<ExamFilter>(() => (filters.some((f) => f.value === params.get('filter')) ? params.get('filter') as ExamFilter : 'todas'))
  const [includeReview, setIncludeReview] = useState(false)
  const [useTimer, setUseTimer] = useState(false)
  const [minutes, setMinutes] = useState('30')
  const [eligibility, setEligibility] = useState<Eligibility | null>(null)
  const [submitError, setSubmitError] = useState<string>()
  const [busy, setBusy] = useState(false)

  useEffect(() => {
    if (courseId == null && courses.data?.length) setCourseId(courses.data[0].id)
  }, [courses.data, courseId])
  useEffect(() => {
    if (courseId == null) return
    api.modules(courseId).then(setModules).catch(() => setModules([]))
  }, [courseId])

  const config: ExamConfig = {
    courseId: courseId ?? 0, moduleIds: moduleIds.length ? moduleIds : null, count: Number(count), order, shuffleOptions,
    filter, includeReview, timeLimitMinutes: useTimer ? Number(minutes) : null, mode,
  }
  const configKey = JSON.stringify({ courseId, moduleIds, filter, includeReview })
  useEffect(() => {
    if (courseId == null) return
    const t = setTimeout(() => {
      api.eligible({ ...config, count: null, timeLimitMinutes: null }).then(setEligibility).catch(() => setEligibility(null))
    }, 200)
    return () => clearTimeout(t)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [configKey])

  if (courses.loading) return <Loading />
  if (courses.error) return <ErrorState message={courses.error} onRetry={courses.reload} />
  if (!courses.data?.length) return <Empty title="No hay cursos disponibles">Importa un banco para generar exámenes.</Empty>

  const countNum = Number(count)
  const countError = !/^\d+$/.test(count.trim()) || countNum < 1
    ? 'Escribe un número entero mayor que cero.'
    : eligibility && countNum > eligibility.max ? `Solo hay ${eligibility.max} preguntas elegibles.` : undefined
  const minutesError = useTimer && (!/^\d+$/.test(minutes.trim()) || Number(minutes) < 1 || Number(minutes) > 600) ? 'Escribe minutos enteros entre 1 y 600.' : undefined
  const toggleModule = (id: number) => setModuleIds((ids) => (ids.includes(id) ? ids.filter((x) => x !== id) : [...ids, id]))

  const submit = async (e: React.FormEvent) => {
    e.preventDefault()
    if (countError || minutesError) return
    setBusy(true); setSubmitError(undefined)
    try {
      const exam = await api.createExam(config)
      navigate(`/examenes/${exam.id}`)
    } catch (err) { setSubmitError(err instanceof Error ? err.message : String(err)) } finally { setBusy(false) }
  }

  return (
    <form onSubmit={submit} className="space-y-6" noValidate>
      <PageHeader title="Nuevo examen" subtitle="Configura un examen o una práctica con las preguntas que quieras." />
      <div className="grid gap-6 lg:grid-cols-3">
        <div className="space-y-6 lg:col-span-2">
          <section className="card space-y-4">
            <div>
              <label className="label" htmlFor="course">Curso</label>
              <select id="course" className="input" value={courseId ?? ''} onChange={(e) => { setCourseId(Number(e.target.value)); setModuleIds([]) }}>
                {courses.data.map((c) => <option key={c.id} value={c.id}>{c.title}</option>)}
              </select>
            </div>
            <div>
              <div className="mb-2 flex items-center justify-between">
                <span className="label mb-0">Módulos {moduleIds.length === 0 && <span className="font-normal text-night/50">(todos)</span>}</span>
                <div className="flex gap-3 text-sm">
                  <button type="button" className="font-semibold text-rose" onClick={() => setModuleIds(modules.map((m) => m.id))}>Seleccionar todos</button>
                  <button type="button" className="font-semibold text-night/60" onClick={() => setModuleIds([])}>Limpiar</button>
                </div>
              </div>
              <div className="grid max-h-72 gap-1 overflow-y-auto rounded-xl border border-night/10 p-2 sm:grid-cols-2">
                {modules.map((m) => (
                  <label key={m.id} className="flex cursor-pointer items-center gap-2 rounded-lg px-2 py-1.5 text-sm hover:bg-blush/15">
                    <input type="checkbox" className="h-4 w-4 accent-[#D95F98]" checked={moduleIds.includes(m.id)} onChange={() => toggleModule(m.id)} />
                    <span className="flex-1">{m.name}</span><span className="text-xs text-night/50">{m.total}</span>
                  </label>
                ))}
              </div>
            </div>
            <fieldset>
              <legend className="label">Preguntas a incluir</legend>
              <div className="grid gap-2 sm:grid-cols-3">
                {filters.map((f) => (
                  <label key={f.value} className={`cursor-pointer rounded-xl border-2 p-3 text-sm ${filter === f.value ? 'border-rose bg-blush/15' : 'border-night/10'}`}>
                    <input type="radio" name="filter" className="sr-only" checked={filter === f.value} onChange={() => setFilter(f.value)} />
                    <span className="block font-semibold">{f.label}</span><span className="text-xs text-night/60">{f.hint}</span>
                  </label>
                ))}
              </div>
            </fieldset>
            <label className="flex items-start gap-2 text-sm">
              <input type="checkbox" className="mt-0.5 h-4 w-4 accent-[#D95F98]" checked={includeReview} onChange={(e) => setIncludeReview(e.target.checked)} />
              <span>Incluir preguntas en revisión <span className="block text-xs text-night/60">Su respuesta no está confirmada; por defecto se excluyen.</span></span>
            </label>
          </section>

          <section className="card grid gap-4 sm:grid-cols-2">
            <fieldset>
              <legend className="label">Modo</legend>
              <div className="space-y-2 text-sm">
                <label className="flex items-start gap-2"><input type="radio" name="mode" className="mt-0.5 accent-[#D95F98]" checked={mode === 'examen'} onChange={() => setMode('examen')} />
                  <span><b>Examen</b><span className="block text-xs text-night/60">Resultados al finalizar.</span></span></label>
                <label className="flex items-start gap-2"><input type="radio" name="mode" className="mt-0.5 accent-[#D95F98]" checked={mode === 'practica'} onChange={() => setMode('practica')} />
                  <span><b>Práctica</b><span className="block text-xs text-night/60">Retroalimentación inmediata por pregunta.</span></span></label>
              </div>
            </fieldset>
            <fieldset>
              <legend className="label">Orden</legend>
              <div className="space-y-2 text-sm">
                <label className="flex items-center gap-2"><input type="radio" name="order" className="accent-[#D95F98]" checked={order === 'aleatorio'} onChange={() => setOrder('aleatorio')} /> Aleatorio</label>
                <label className="flex items-center gap-2"><input type="radio" name="order" className="accent-[#D95F98]" checked={order === 'secuencial'} onChange={() => setOrder('secuencial')} /> Secuencial (por módulo)</label>
                <label className="flex items-center gap-2"><input type="checkbox" className="accent-[#D95F98]" checked={shuffleOptions} onChange={(e) => setShuffleOptions(e.target.checked)} /> Mezclar opciones</label>
              </div>
            </fieldset>
          </section>
        </div>

        <aside className="space-y-4">
          <section className="card space-y-4 lg:sticky lg:top-6">
            <div className="rounded-xl bg-night p-4 text-white">
              <p className="text-sm text-white/70">Preguntas elegibles</p>
              <p className="text-3xl font-bold">{eligibility ? eligibility.eligible : '…'}</p>
              {eligibility && eligibility.excludedForReview > 0 && (
                <p className="mt-1 flex items-center gap-1 text-xs text-blush"><Info className="h-3 w-3" aria-hidden /> {eligibility.excludedForReview} excluidas por estar en revisión</p>
              )}
            </div>
            <div>
              <label className="label" htmlFor="count">Cantidad de preguntas</label>
              <input id="count" className="input" inputMode="numeric" value={count} onChange={(e) => setCount(e.target.value)} aria-invalid={!!countError} aria-describedby="count-help" />
              <div className="mt-2 flex flex-wrap gap-1">
                {[10, 20, 30, 50].map((n) => (
                  <button key={n} type="button" className="chip bg-blush/30 text-night hover:bg-blush/60" onClick={() => setCount(String(n))}>{n}</button>
                ))}
                {eligibility && <button type="button" className="chip bg-night/10 text-night hover:bg-night/20" onClick={() => setCount(String(eligibility.max))}>Todas ({eligibility.max})</button>}
              </div>
              <p id="count-help" className={`mt-1 text-xs ${countError ? 'text-coral' : 'text-night/50'}`}>{countError ?? 'Sin repeticiones dentro del examen.'}</p>
            </div>
            <div>
              <label className="flex items-center gap-2 text-sm font-medium"><input type="checkbox" className="accent-[#D95F98]" checked={useTimer} onChange={(e) => setUseTimer(e.target.checked)} /> Tiempo límite</label>
              {useTimer && (
                <div className="mt-2">
                  <label className="sr-only" htmlFor="minutes">Minutos</label>
                  <div className="flex items-center gap-2"><input id="minutes" className="input" inputMode="numeric" value={minutes} onChange={(e) => setMinutes(e.target.value)} aria-invalid={!!minutesError} /><span className="text-sm">min</span></div>
                  {minutesError && <p className="mt-1 text-xs text-coral">{minutesError}</p>}
                </div>
              )}
            </div>
            {submitError && <p role="alert" className="rounded-lg bg-coral/10 p-2 text-sm text-coral">{submitError}</p>}
            <button type="submit" className="btn-primary w-full" disabled={busy || !!countError || !!minutesError || eligibility?.eligible === 0}>
              <ClipboardList className="h-4 w-4" /> {mode === 'examen' ? 'Comenzar examen' : 'Comenzar práctica'}
            </button>
          </section>
        </aside>
      </div>
    </form>
  )
}
