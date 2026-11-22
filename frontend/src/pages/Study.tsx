import { ChevronLeft, ChevronRight, Heart, Repeat, RotateCcw } from 'lucide-react'
import { useEffect, useMemo, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { api, type CheckResult, type QuestionPublic } from '../api'
import { QuestionView } from '../components/QuestionView'
import { Empty, ErrorState, Loading, MasteryChip, PageHeader, ProgressBar, useAsync } from '../components/ui'

type Filter = 'todas' | 'pendientes' | 'errores'

export function StudyPage() {
  const moduleId = Number(useParams().moduleId)
  const { data, error, loading, reload, setData } = useAsync(() => api.moduleQuestions(moduleId), [moduleId])
  const storageKey = `studyhub.study.${moduleId}`
  const [filter, setFilter] = useState<Filter>('todas')
  const [filterIds, setFilterIds] = useState<Set<number>>(new Set())
  const [index, setIndex] = useState(() => Number(localStorage.getItem(storageKey) ?? 0))
  const [selected, setSelected] = useState<string[]>([])
  const [result, setResult] = useState<CheckResult | null>(null)
  const [busy, setBusy] = useState(false)
  const [actionError, setActionError] = useState<string>()

  const list = useMemo(() => {
    const qs = data ?? []
    return filter === 'todas' ? qs : qs.filter((q) => filterIds.has(q.id))
  }, [data, filter, filterIds])
  const safeIndex = Math.min(index, Math.max(0, list.length - 1))
  const q = list[safeIndex]

  useEffect(() => { localStorage.setItem(storageKey, String(safeIndex)) }, [safeIndex, storageKey])
  useEffect(() => { setSelected([]); setResult(null); setActionError(undefined) }, [q?.id])

  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  if (data.length === 0) return <Empty title="Este módulo no tiene preguntas" />

  const update = (id: number, patch: Partial<QuestionPublic>) => setData(data.map((x) => (x.id === id ? { ...x, ...patch } : x)))
  const applyFilter = (f: Filter) => {
    const matches = (x: QuestionPublic) => f === 'pendientes' ? x.progress.mastery !== 'dominada' : x.progress.mastery === 'con_errores'
    setFilterIds(new Set(data.filter(matches).map((x) => x.id)))
    setFilter(f); setIndex(0)
  }
  const go = (i: number) => setIndex(Math.max(0, Math.min(list.length - 1, i)))
  const check = async () => {
    if (!q) return
    setBusy(true); setActionError(undefined)
    try {
      const r = await api.check(q.id, selected)
      setResult(r)
      update(q.id, { progress: r.progress })
    } catch (e) { setActionError(e instanceof Error ? e.message : String(e)) } finally { setBusy(false) }
  }
  const toggleMark = async (kind: 'favorito' | 'repasar') => {
    if (!q) return
    const field = kind === 'favorito' ? 'isFavorite' : 'isMarkedForReview'
    const next = !q[field]
    update(q.id, { [field]: next })
    try { await api.setMark(kind, q.id, next) } catch (e) { update(q.id, { [field]: !next }); setActionError(e instanceof Error ? e.message : String(e)) }
  }
  const mastered = data.filter((x) => x.progress.mastery === 'dominada').length

  return (
    <div className="space-y-6">
      <PageHeader title={data[0].moduleName}
        subtitle={<Link to={`/cursos/${data[0].courseId}`} className="hover:text-rose">← Volver al curso</Link>}
        actions={
          <div className="flex rounded-xl border border-night/15 bg-white p-1 text-sm" role="group" aria-label="Filtrar preguntas">
            {(['todas', 'pendientes', 'errores'] as Filter[]).map((f) => (
              <button key={f} onClick={() => applyFilter(f)} aria-pressed={filter === f}
                className={`rounded-lg px-3 py-1.5 font-medium ${filter === f ? 'bg-night text-white' : 'text-night/70 hover:bg-blush/20'}`}>
                {f === 'todas' ? 'Todas' : f === 'pendientes' ? 'Sin dominar' : 'Con errores'}
              </button>
            ))}
          </div>
        } />
      <div className="space-y-1">
        <div className="flex justify-between text-xs text-night/60"><span>{mastered} de {data.length} dominadas</span><span>Pregunta {list.length ? safeIndex + 1 : 0} de {list.length}</span></div>
        <ProgressBar value={(mastered / data.length) * 100} label="Dominio del módulo" />
      </div>

      {!q ? <Empty title="No hay preguntas con este filtro">Cambia el filtro para ver otras preguntas.</Empty> : (
        <div className="card space-y-5">
          <QuestionView key={q.id} text={q.text} type={q.type} options={q.options} selected={selected} onChange={setSelected}
            feedback={result?.feedback} needsReview={q.needsReview} reviewNotes={q.reviewNotes}
            header={
              <div className="flex flex-wrap items-center justify-between gap-2">
                <div className="flex items-center gap-2 text-sm text-night/60">
                  <span className="font-semibold">Pregunta #{q.originalId}</span>
                  <MasteryChip mastery={q.progress.mastery} />
                  {q.progress.attempts > 0 && <span>{q.progress.correctAttempts}/{q.progress.attempts} aciertos</span>}
                </div>
                <div className="flex gap-2">
                  <button className={`btn-ghost px-3 ${q.isFavorite ? 'border-rose text-rose' : ''}`} onClick={() => toggleMark('favorito')} aria-pressed={q.isFavorite}>
                    <Heart className={`h-4 w-4 ${q.isFavorite ? 'fill-rose' : ''}`} aria-hidden /> Favorita
                  </button>
                  <button className={`btn-ghost px-3 ${q.isMarkedForReview ? 'border-amber text-[#9A5A0B]' : ''}`} onClick={() => toggleMark('repasar')} aria-pressed={q.isMarkedForReview}>
                    <Repeat className="h-4 w-4" aria-hidden /> Repasar
                  </button>
                </div>
              </div>
            } />
          {actionError && <p role="alert" className="text-sm text-coral">{actionError}</p>}
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-night/5 pt-4">
            <button className="btn-ghost" onClick={() => go(safeIndex - 1)} disabled={safeIndex === 0}><ChevronLeft className="h-4 w-4" /> Anterior</button>
            <div className="flex gap-2">
              {result ? (
                <button className="btn-ghost" onClick={() => { setResult(null); setSelected([]) }}><RotateCcw className="h-4 w-4" /> Reintentar</button>
              ) : (
                <button className="btn-primary" onClick={check} disabled={busy || selected.length === 0}>Comprobar</button>
              )}
              <button className="btn-dark" onClick={() => go(safeIndex + 1)} disabled={safeIndex >= list.length - 1}>Siguiente <ChevronRight className="h-4 w-4" /></button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
