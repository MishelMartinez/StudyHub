import { ClipboardList } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, ErrorState, Loading, MasteryChip, PageHeader, fmtDate, useAsync } from '../components/ui'

export function ErrorLogPage() {
  const { data, error, loading, reload } = useAsync(() => api.errors(), [])
  const [moduleId, setModuleId] = useState<number | 'all'>('all')
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  const entries = data.entries.filter((e) => moduleId === 'all' || e.moduleId === moduleId)
  const courseId = data.entries[0]?.courseId

  return (
    <div className="space-y-6">
      <PageHeader title="Error Log" subtitle="Preguntas que has fallado alguna vez, con tu última respuesta y la regla para memorizar."
        actions={courseId && <Link to={`/examenes/nuevo?courseId=${courseId}&filter=errores`} className="btn-primary"><ClipboardList className="h-4 w-4" /> Examen de errores</Link>} />
      {data.entries.length === 0 ? <Empty title="¡Sin errores registrados!">Cuando falles una pregunta aparecerá aquí para que la repases.</Empty> : (
        <>
          <div className="flex flex-wrap gap-2">
            <button className={`chip px-3 py-1 ${moduleId === 'all' ? 'bg-night text-white' : 'bg-white text-night ring-1 ring-night/15'}`} onClick={() => setModuleId('all')}>Todos ({data.entries.length})</button>
            {data.byModule.map((m) => (
              <button key={m.moduleId} className={`chip px-3 py-1 ${moduleId === m.moduleId ? 'bg-night text-white' : 'bg-white text-night ring-1 ring-night/15'}`} onClick={() => setModuleId(m.moduleId)}>
                {m.module} ({m.questions})
              </button>
            ))}
          </div>
          <div className="space-y-3">
            {entries.map((e) => (
              <div key={e.questionId} className="card space-y-2">
                <div className="flex flex-wrap items-center gap-2 text-xs text-night/60">
                  <span className="font-semibold">#{e.originalId}</span><span>{e.module}</span><MasteryChip mastery={e.mastery} />
                  {e.needsReview && <span className="chip bg-amber/15 text-[#9A5A0B]">En revisión</span>}
                  <span className="ml-auto">{e.errors} error(es) en {e.attempts} intento(s) · último error {fmtDate(e.lastErrorAt)}</span>
                </div>
                <p className="font-semibold">{e.text}</p>
                <p className="text-sm"><span className="text-coral">Tu última respuesta incorrecta: {e.lastSelected.join(', ') || '—'}</span> · <span className="text-emerald-700">Correcta: {e.correctAnswers.join(', ')}</span></p>
                {e.memoryRule && <p className="rounded-lg bg-amber/10 p-2 text-sm"><b>Regla:</b> {e.memoryRule}</p>}
                <Link to={`/estudiar/${e.moduleId}`} className="text-sm font-semibold text-rose">Estudiar módulo →</Link>
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  )
}
