import { ArrowRight, BookOpen, CheckCircle2, ClipboardList, XCircle } from 'lucide-react'
import { Link } from 'react-router-dom'
import { api } from '../api'
import { Empty, ErrorState, Loading, PageHeader, ProgressBar, Stat, fmtDate, pct, useAsync } from '../components/ui'

export function DashboardPage() {
  const { data, error, loading, reload } = useAsync(api.dashboard, [])
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  const totals = data.courses.reduce(
    (a, c) => ({ total: a.total + c.total, mastered: a.mastered + c.mastered, attempted: a.attempted + c.attempted, errors: a.errors + c.withErrors }),
    { total: 0, mastered: 0, attempted: 0, errors: 0 })

  return (
    <div className="space-y-8">
      <PageHeader title="Tu panel de estudio" subtitle="Practica por módulo, genera exámenes y repasa tus errores."
        actions={<>
          {data.continueStudy && (
            <Link to={`/estudiar/${data.continueStudy.moduleId}`} className="btn-primary"><BookOpen className="h-4 w-4" /> Continuar: {data.continueStudy.moduleName}</Link>
          )}
          <Link to="/examenes/nuevo" className="btn-dark"><ClipboardList className="h-4 w-4" /> Nuevo examen</Link>
        </>} />

      {data.courses.length === 0 ? (
        <Empty title="Aún no hay bancos de preguntas">
          <Link to="/importar" className="font-semibold text-rose underline">Importa un banco JSON</Link> para empezar a estudiar.
        </Empty>
      ) : (
        <>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat label="Preguntas" value={totals.total} hint={`${totals.attempted} practicadas`} />
            <Stat label="Dominadas" value={totals.mastered} hint="2 aciertos consecutivos" tone="rose" />
            <Stat label="Con errores" value={totals.errors} hint="Último intento incorrecto" tone="coral" />
            <Stat label="Precisión" value={data.totalAttempts ? pct(data.accuracy) : '—'} hint={`${data.correctAttempts}/${data.totalAttempts} intentos correctos`} tone="amber" />
          </div>

          <section aria-labelledby="cursos-t">
            <h2 id="cursos-t" className="mb-3 text-lg font-bold">Cursos</h2>
            <div className="grid gap-4 md:grid-cols-2">
              {data.courses.map((c) => (
                <Link key={c.id} to={`/cursos/${c.id}`} className="card group transition hover:border-rose/50 hover:shadow-md">
                  <div className="flex items-start justify-between gap-2">
                    <div>
                      <p className="font-bold">{c.title}</p>
                      <p className="muted">{c.moduleCount} módulos · {c.total} preguntas · {c.inReview} en revisión</p>
                    </div>
                    <ArrowRight className="h-5 w-5 text-rose transition group-hover:translate-x-1" aria-hidden />
                  </div>
                  <div className="mt-4 space-y-1">
                    <div className="flex justify-between text-xs text-night/60"><span>Dominio</span><span>{pct(c.progressPercentage)}</span></div>
                    <ProgressBar value={c.progressPercentage} label={`Dominio de ${c.title}`} />
                  </div>
                </Link>
              ))}
            </div>
          </section>

          <div className="grid gap-6 lg:grid-cols-2">
            <section className="card min-w-0" aria-labelledby="act-t">
              <h2 id="act-t" className="mb-3 font-bold">Actividad reciente</h2>
              {data.recentActivity.length === 0 ? <p className="muted">Todavía no has respondido preguntas.</p> : (
                <ul className="divide-y divide-night/5">
                  {data.recentActivity.map((a, i) => (
                    <li key={i} className="flex items-start gap-3 py-2 text-sm">
                      {a.isCorrect ? <CheckCircle2 className="mt-0.5 h-4 w-4 shrink-0 text-emerald-600" aria-label="Correcta" /> : <XCircle className="mt-0.5 h-4 w-4 shrink-0 text-coral" aria-label="Incorrecta" />}
                      <div className="min-w-0 flex-1">
                        <p className="truncate">#{a.originalId} · {a.text}</p>
                        <p className="text-xs text-night/50">{a.module} · {a.context === 'estudio' ? 'Estudio' : a.context === 'practica' ? 'Práctica' : 'Examen'} · {fmtDate(a.at)}</p>
                      </div>
                    </li>
                  ))}
                </ul>
              )}
            </section>
            <section className="card min-w-0" aria-labelledby="ex-t">
              <div className="mb-3 flex items-center justify-between">
                <h2 id="ex-t" className="font-bold">Exámenes recientes</h2>
                <Link to="/examenes" className="text-sm font-semibold text-rose">Ver todos</Link>
              </div>
              {data.recentExams.length === 0 ? <p className="muted">Aún no has generado exámenes.</p> : (
                <ul className="divide-y divide-night/5">
                  {data.recentExams.map((e) => (
                    <li key={e.id}>
                      <Link to={`/examenes/${e.id}`} className="flex items-center justify-between gap-3 py-2 text-sm hover:text-rose">
                        <span className="truncate">{e.title}</span>
                        {e.status === 'finalizado'
                          ? <span className={`chip ${e.passed ? 'bg-emerald-50 text-emerald-700' : 'bg-coral/10 text-coral'}`}>{pct(e.percentage)}</span>
                          : <span className="chip bg-amber/15 text-[#9A5A0B]">En curso</span>}
                      </Link>
                    </li>
                  ))}
                </ul>
              )}
            </section>
          </div>
        </>
      )}
    </div>
  )
}
