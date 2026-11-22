import { CheckCircle2, ChevronLeft, ChevronRight, Clock, Flag, RotateCcw, XCircle } from 'lucide-react'
import { useCallback, useEffect, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError, api, type Exam } from '../api'
import { FeedbackPanel, QuestionView } from '../components/QuestionView'
import { ErrorState, Loading, PageHeader, ProgressBar, Stat, fmtDuration, pct, useAsync } from '../components/ui'

export function ExamPage() {
  const examId = Number(useParams().examId)
  const { data, error, loading, reload, setData } = useAsync(() => api.exam(examId), [examId])
  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />
  return data.status === 'finalizado' ? <ExamResults exam={data} /> : <ExamRunner exam={data} setExam={setData} />
}

function useCountdown(deadline: string | null) {
  const [left, setLeft] = useState<number | null>(null)
  useEffect(() => {
    if (!deadline) return
    const tick = () => setLeft(Math.max(0, Math.round((new Date(deadline).getTime() - Date.now()) / 1000)))
    tick()
    const t = setInterval(tick, 1000)
    return () => clearInterval(t)
  }, [deadline])
  return left
}

function ExamRunner({ exam, setExam }: { exam: Exam; setExam: (e: Exam) => void }) {
  const storageKey = `studyhub.exam.${exam.id}.pos`
  const [pos, setPos] = useState(() => Math.min(exam.total, Math.max(1, Number(localStorage.getItem(storageKey) ?? 1))))
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string>()
  const [confirming, setConfirming] = useState(false)
  const left = useCountdown(exam.deadline)
  const q = exam.questions[pos - 1]
  const isPractice = exam.mode === 'practica'

  useEffect(() => { localStorage.setItem(storageKey, String(pos)) }, [pos, storageKey])

  const run = useCallback(async (fn: () => Promise<Exam>) => {
    setBusy(true); setErr(undefined)
    try { setExam(await fn()) } catch (e) {
      if (e instanceof ApiError && e.status === 409) setExam(await api.exam(exam.id))
      else setErr(e instanceof Error ? e.message : String(e))
    } finally { setBusy(false) }
  }, [exam.id, setExam])

  const finish = useCallback(() => run(() => api.finish(exam.id)), [run, exam.id])
  useEffect(() => { if (left === 0) void finish() }, [left, finish])

  const select = (selected: string[]) => {
    setExam({ ...exam, questions: exam.questions.map((x) => (x.position === pos ? { ...x, selected } : x)) })
    void run(() => api.answer(exam.id, pos, { selected }))
  }
  const answered = exam.questions.filter((x) => x.selected.length > 0).length
  const unanswered = exam.total - answered

  return (
    <div className="space-y-6">
      <PageHeader title={exam.title} subtitle={`${isPractice ? 'Práctica: retroalimentación inmediata' : 'Examen: los resultados se muestran al finalizar'} · ${answered}/${exam.total} respondidas`}
        actions={left != null && (
          <div className={`flex items-center gap-2 rounded-xl px-4 py-2 font-mono text-lg font-bold ${left < 60 ? 'bg-coral text-white' : 'bg-night text-white'}`} role="timer" aria-live="off">
            <Clock className="h-5 w-5" aria-hidden /> {String(Math.floor(left / 60)).padStart(2, '0')}:{String(left % 60).padStart(2, '0')}
          </div>
        )} />
      <ProgressBar value={(answered / exam.total) * 100} label="Avance del examen" />

      <div className="grid gap-6 lg:grid-cols-[1fr_220px]">
        <div className="card space-y-5">
          <QuestionView key={q.position} text={q.text} type={q.type} options={q.options} selected={q.selected} onChange={select}
            feedback={q.feedback} needsReview={q.needsReview} reviewNotes={q.reviewNotes}
            header={
              <div className="flex flex-wrap items-center justify-between gap-2 text-sm text-night/60">
                <span><b>Pregunta {q.position} de {exam.total}</b> · {q.moduleName}</span>
                <button className={`btn-ghost px-3 ${q.marked ? 'border-amber text-[#9A5A0B]' : ''}`} aria-pressed={q.marked}
                  onClick={() => run(() => api.answer(exam.id, pos, { marked: !q.marked }))}>
                  <Flag className="h-4 w-4" aria-hidden /> {q.marked ? 'Marcada' : 'Marcar para revisar'}
                </button>
              </div>
            } />
          {err && <p role="alert" className="text-sm text-coral">{err}</p>}
          <div className="flex flex-wrap items-center justify-between gap-2 border-t border-night/5 pt-4">
            <button className="btn-ghost" onClick={() => setPos(pos - 1)} disabled={pos === 1}><ChevronLeft className="h-4 w-4" /> Anterior</button>
            <div className="flex gap-2">
              {isPractice && !q.checked && (
                <button className="btn-primary" disabled={busy || q.selected.length === 0} onClick={() => run(() => api.checkExam(exam.id, pos))}>Comprobar</button>
              )}
              {pos < exam.total
                ? <button className="btn-dark" onClick={() => setPos(pos + 1)}>Siguiente <ChevronRight className="h-4 w-4" /></button>
                : <button className="btn-primary" onClick={() => setConfirming(true)}>Finalizar</button>}
            </div>
          </div>
        </div>

        <aside className="card h-fit space-y-4">
          <p className="text-sm font-semibold">Navegación</p>
          <div className="grid grid-cols-5 gap-1.5">
            {exam.questions.map((x) => {
              let cls = 'border-night/15 bg-white text-night'
              if (x.selected.length) cls = 'border-night bg-night text-white'
              if (x.feedback) cls = x.feedback.isCorrect ? 'border-emerald-600 bg-emerald-600 text-white' : 'border-coral bg-coral text-white'
              return (
                <button key={x.position} onClick={() => setPos(x.position)} aria-label={`Ir a la pregunta ${x.position}`} aria-current={x.position === pos}
                  className={`relative h-9 rounded-lg border text-xs font-semibold ${cls} ${x.position === pos ? 'ring-2 ring-rose ring-offset-1' : ''}`}>
                  {x.position}
                  {x.marked && <span className="absolute -right-0.5 -top-0.5 h-2.5 w-2.5 rounded-full bg-amber" aria-hidden />}
                </button>
              )
            })}
          </div>
          <ul className="space-y-1 text-xs text-night/60">
            <li className="flex items-center gap-2"><span className="h-3 w-3 rounded bg-night" /> Respondida</li>
            <li className="flex items-center gap-2"><span className="h-3 w-3 rounded border border-night/30" /> Sin responder</li>
            <li className="flex items-center gap-2"><span className="h-2.5 w-2.5 rounded-full bg-amber" /> Marcada</li>
          </ul>
          <button className="btn-primary w-full" onClick={() => setConfirming(true)}>Finalizar</button>
        </aside>
      </div>

      {confirming && (
        <div className="fixed inset-0 z-40 grid place-items-center bg-night/50 p-4" role="dialog" aria-modal="true" aria-labelledby="confirm-t">
          <div className="card w-full max-w-md space-y-4">
            <h2 id="confirm-t" className="text-lg font-bold">¿Finalizar {isPractice ? 'la práctica' : 'el examen'}?</h2>
            <p className="text-sm text-night/70">
              {unanswered > 0 ? `Tienes ${unanswered} pregunta(s) sin responder; contarán como incorrectas.` : 'Respondiste todas las preguntas.'} Una vez finalizado no podrás cambiar tus respuestas.
            </p>
            <div className="flex justify-end gap-2">
              <button className="btn-ghost" onClick={() => setConfirming(false)}>Seguir respondiendo</button>
              <button className="btn-primary" disabled={busy} onClick={() => { setConfirming(false); void finish() }}>Finalizar</button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

type ReviewFilter = 'todas' | 'incorrectas' | 'sin_responder' | 'correctas'

function ExamResults({ exam }: { exam: Exam }) {
  const navigate = useNavigate()
  const r = exam.result!
  const [filter, setFilter] = useState<ReviewFilter>('todas')
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string>()
  const failed = r.incorrect + r.unanswered
  const shown = exam.questions.filter((q) => {
    const unans = q.selected.length === 0
    if (filter === 'sin_responder') return unans
    if (filter === 'incorrectas') return !unans && !q.feedback?.isCorrect
    if (filter === 'correctas') return q.feedback?.isCorrect
    return true
  })
  const retry = async () => {
    setBusy(true); setErr(undefined)
    try { navigate(`/examenes/${(await api.retryErrors(exam.id)).id}`) } catch (e) { setErr(e instanceof Error ? e.message : String(e)) } finally { setBusy(false) }
  }

  return (
    <div className="space-y-6">
      <PageHeader title={`Resultados · ${exam.title}`} subtitle={`${exam.mode === 'examen' ? 'Examen' : 'Práctica'} · ${exam.courseTitle}`}
        actions={<>
          {failed > 0 && <button className="btn-primary" disabled={busy} onClick={retry}><RotateCcw className="h-4 w-4" /> Repetir errores ({failed})</button>}
          <Link to="/examenes/nuevo" className="btn-ghost">Nuevo examen</Link>
        </>} />
      {err && <p role="alert" className="text-sm text-coral">{err}</p>}

      <div className={`card flex flex-wrap items-center gap-6 ${r.passed ? 'border-emerald-200 bg-emerald-50/60' : 'border-coral/30 bg-coral/5'}`}>
        <div className={`grid h-24 w-24 place-items-center rounded-full text-2xl font-bold text-white ${r.passed ? 'bg-emerald-600' : 'bg-coral'}`}>{pct(r.percentage)}</div>
        <div>
          <p className="text-xl font-bold">{r.passed ? '¡Aprobado!' : 'No aprobado'}</p>
          <p className="muted">Umbral de aprobación: {r.passThreshold}% · Tiempo: {fmtDuration(r.durationSeconds)}</p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-4">
        <Stat label="Total" value={r.total} />
        <Stat label="Aciertos" value={r.correct} tone="rose" />
        <Stat label="Errores" value={r.incorrect} tone="coral" />
        <Stat label="Sin responder" value={r.unanswered} tone="amber" hint="Cuentan como incorrectas" />
      </div>

      <section className="card">
        <h2 className="mb-4 font-bold">Desempeño por módulo</h2>
        <div className="space-y-3">
          {r.byModule.map((m) => (
            <div key={m.moduleId}>
              <div className="mb-1 flex justify-between text-sm"><span>{m.module}</span><span className="font-semibold">{m.correct}/{m.total} · {pct(m.percentage)}</span></div>
              <ProgressBar value={m.percentage} tone={m.percentage >= r.passThreshold ? 'rose' : 'coral'} label={m.module} />
            </div>
          ))}
        </div>
      </section>

      <section className="space-y-4">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-lg font-bold">Revisión pregunta por pregunta</h2>
          <div className="flex rounded-xl border border-night/15 bg-white p-1 text-sm" role="group" aria-label="Filtrar revisión">
            {([['todas', 'Todas'], ['incorrectas', 'Incorrectas'], ['sin_responder', 'Sin responder'], ['correctas', 'Correctas']] as [ReviewFilter, string][]).map(([v, l]) => (
              <button key={v} aria-pressed={filter === v} onClick={() => setFilter(v)} className={`rounded-lg px-3 py-1.5 font-medium ${filter === v ? 'bg-night text-white' : 'text-night/70 hover:bg-blush/20'}`}>{l}</button>
            ))}
          </div>
        </div>
        {shown.length === 0 && <p className="muted">No hay preguntas en esta categoría.</p>}
        {shown.map((q) => (
          <details key={q.position} className="card group">
            <summary className="flex cursor-pointer list-none items-start gap-3">
              {q.feedback?.isCorrect ? <CheckCircle2 className="mt-0.5 h-5 w-5 shrink-0 text-emerald-600" aria-label="Correcta" /> : <XCircle className="mt-0.5 h-5 w-5 shrink-0 text-coral" aria-label="Incorrecta" />}
              <div className="flex-1">
                <p className="font-semibold">{q.position}. {q.text}</p>
                <p className="text-xs text-night/50">#{q.originalId} · {q.moduleName}{q.selected.length === 0 ? ' · Sin responder' : ` · Tu respuesta: ${q.selected.join(', ')}`}</p>
              </div>
              <ChevronRight className="h-5 w-5 text-night/40 transition group-open:rotate-90" aria-hidden />
            </summary>
            {q.feedback && (
              <div className="mt-4 space-y-2 border-t border-night/5 pt-4">
                <ul className="space-y-1 text-sm">
                  {q.feedback.options.map((o) => (
                    <li key={o.key} className={`rounded-lg px-3 py-2 ${o.isCorrect ? 'bg-emerald-50 text-emerald-800' : o.selected ? 'bg-coral/10 text-coral' : ''}`}>
                      <b>{o.key}.</b> {o.text} {o.isCorrect && '✓'} {o.selected && !o.isCorrect && '(tu respuesta)'}
                    </li>
                  ))}
                </ul>
                <FeedbackPanel feedback={q.feedback} />
              </div>
            )}
          </details>
        ))}
      </section>
    </div>
  )
}
