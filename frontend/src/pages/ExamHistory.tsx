import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, ErrorState, Loading, PageHeader, fmtDate, pct, useAsync } from '../components/ui'

export function ExamHistoryPage() {
  const { data, error, loading, reload } = useAsync(api.exams, [])
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  return (
    <div>
      <PageHeader title="Historial de exámenes" actions={<Link to="/examenes/nuevo" className="btn-primary">Nuevo examen</Link>} />
      {data.length === 0 ? <Empty title="Aún no hay exámenes">Genera tu primer examen para ver tu historial.</Empty> : (
        <div className="card overflow-x-auto p-0">
          <table className="w-full text-left text-sm">
            <thead className="bg-night/[0.03] text-xs uppercase text-night/60">
              <tr><th className="px-4 py-3">Examen</th><th className="px-4 py-3">Modo</th><th className="px-4 py-3">Fecha</th><th className="px-4 py-3">Preguntas</th><th className="px-4 py-3">Resultado</th></tr>
            </thead>
            <tbody className="divide-y divide-night/5">
              {data.map((e) => (
                <tr key={e.id} className="hover:bg-blush/10">
                  <td className="px-4 py-3"><Link className="font-semibold hover:text-rose" to={`/examenes/${e.id}`}>{e.title}</Link></td>
                  <td className="px-4 py-3">{e.mode === 'examen' ? 'Examen' : 'Práctica'}</td>
                  <td className="px-4 py-3">{fmtDate(e.createdAt)}</td>
                  <td className="px-4 py-3">{e.total}</td>
                  <td className="px-4 py-3">
                    {e.status === 'finalizado'
                      ? <span className={`chip ${e.passed ? 'bg-emerald-50 text-emerald-700' : 'bg-coral/10 text-coral'}`}>{e.correct}/{e.total} · {pct(e.percentage)}</span>
                      : <Link to={`/examenes/${e.id}`} className="chip bg-amber/15 text-[#9A5A0B]">En curso · continuar</Link>}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  )
}
