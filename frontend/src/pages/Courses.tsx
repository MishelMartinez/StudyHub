import { AlertTriangle, BookOpen, ClipboardList } from 'lucide-react'
import { Link, useParams } from 'react-router-dom'
import { api } from '../api'
import { Empty, ErrorState, Loading, PageHeader, ProgressBar, pct, useAsync } from '../components/ui'

export function CoursesPage() {
  const { data, error, loading, reload } = useAsync(api.courses, [])
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  return (
    <div>
      <PageHeader title="Cursos" subtitle="Los cursos y módulos se generan automáticamente a partir de los bancos importados." />
      {data.length === 0 ? <Empty title="No hay cursos"><Link className="text-rose underline" to="/importar">Importa un banco</Link></Empty> : (
        <div className="grid gap-4 md:grid-cols-2">
          {data.map((c) => (
            <Link key={c.id} to={`/cursos/${c.id}`} className="card hover:border-rose/50">
              <p className="text-lg font-bold">{c.title}</p>
              <p className="muted">{c.moduleCount} módulos · {c.total} preguntas · versión {c.bankVersions}</p>
              <div className="mt-4"><ProgressBar value={c.progressPercentage} label="Dominio" /></div>
              <p className="mt-2 text-xs text-night/60">{c.mastered} dominadas · {c.withErrors} con errores · {c.inReview} en revisión</p>
            </Link>
          ))}
        </div>
      )}
    </div>
  )
}

export function CoursePage() {
  const courseId = Number(useParams().courseId)
  const { data, error, loading, reload } = useAsync(async () => {
    const [courses, modules] = await Promise.all([api.courses(), api.modules(courseId)])
    return { course: courses.find((c) => c.id === courseId), modules }
  }, [courseId])
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  if (!data.course) return <Empty title="Curso no encontrado" />
  const { course, modules } = data
  return (
    <div>
      <PageHeader title={course.title} subtitle={`${course.moduleCount} módulos · ${course.total} preguntas · ${pct(course.progressPercentage)} dominado`}
        actions={<Link to={`/examenes/nuevo?courseId=${course.id}`} className="btn-dark"><ClipboardList className="h-4 w-4" /> Examen del curso</Link>} />
      <div className="grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
        {modules.map((m) => (
          <div key={m.id} className="card flex flex-col">
            <div className="flex items-start justify-between gap-2">
              <p className="font-bold leading-snug">{m.name}</p>
              {m.inReview > 0 && (
                <span className="chip shrink-0 bg-amber/15 text-[#9A5A0B]" title="Preguntas en revisión"><AlertTriangle className="h-3 w-3" aria-hidden /> {m.inReview}</span>
              )}
            </div>
            <p className="muted mt-1">{m.total} preguntas · {m.attempted} practicadas</p>
            <div className="mt-3"><ProgressBar value={m.progressPercentage} label={`Dominio de ${m.name}`} /></div>
            <p className="mt-2 text-xs text-night/60">{m.mastered} dominadas · {m.withErrors} con errores</p>
            <div className="mt-4 flex gap-2 pt-1">
              <Link to={`/estudiar/${m.id}`} className="btn-primary flex-1"><BookOpen className="h-4 w-4" /> Estudiar</Link>
              <Link to={`/examenes/nuevo?courseId=${course.id}&moduleId=${m.id}`} className="btn-ghost" aria-label={`Examen de ${m.name}`}><ClipboardList className="h-4 w-4" /></Link>
            </div>
          </div>
        ))}
      </div>
    </div>
  )
}
