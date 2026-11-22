import { Heart, Repeat } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, ErrorState, Loading, MasteryChip, PageHeader, useAsync } from '../components/ui'

export function MarksPage({ kind }: { kind: 'favorito' | 'repasar' }) {
  const { data, error, loading, reload, setData } = useAsync(() => api.marked(kind), [kind])
  const fav = kind === 'favorito'
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  const remove = async (id: number) => {
    await api.setMark(kind, id, false)
    setData(data.filter((q) => q.id !== id))
  }
  const courseId = data[0]?.courseId
  return (
    <div className="space-y-6">
      <PageHeader title={fav ? 'Preguntas favoritas' : 'Preguntas para repasar'}
        actions={courseId && <Link to={`/examenes/nuevo?courseId=${courseId}&filter=${fav ? 'favoritas' : 'repaso'}`} className="btn-primary">Generar examen</Link>} />
      {data.length === 0 ? (
        <Empty title={fav ? 'No tienes favoritas' : 'No hay preguntas marcadas para repasar'}>Márcalas desde la vista de estudio de cualquier módulo.</Empty>
      ) : (
        <div className="space-y-3">
          {data.map((q) => (
            <div key={q.id} className="card flex flex-wrap items-start gap-3">
              {fav ? <Heart className="mt-1 h-5 w-5 fill-rose text-rose" aria-hidden /> : <Repeat className="mt-1 h-5 w-5 text-amber" aria-hidden />}
              <div className="min-w-0 flex-1">
                <div className="flex flex-wrap items-center gap-2 text-xs text-night/60"><span className="font-semibold">#{q.originalId}</span><span>{q.moduleName}</span><MasteryChip mastery={q.progress.mastery} /></div>
                <p className="mt-1 font-medium">{q.text}</p>
              </div>
              <div className="flex gap-2">
                <Link to={`/estudiar/${q.moduleId}`} className="btn-ghost">Estudiar</Link>
                <button className="btn-ghost text-coral" onClick={() => remove(q.id)}>Quitar</button>
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  )
}
