import { AlertTriangle, CheckCircle2, FileJson, Upload } from 'lucide-react'
import { useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError, api, type ImportReport } from '../api'
import { PageHeader } from '../components/ui'

export function ImportPage() {
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState<ImportReport | null>(null)
  const [result, setResult] = useState<ImportReport | null>(null)
  const [busy, setBusy] = useState(false)
  const [err, setErr] = useState<string>()

  const choose = async (f: File | null) => {
    setFile(f); setPreview(null); setResult(null); setErr(undefined)
    if (!f) return
    setBusy(true)
    try { setPreview(await api.preview(f)) } catch (e) { setErr(e instanceof Error ? e.message : String(e)) } finally { setBusy(false) }
  }
  const commit = async () => {
    if (!file) return
    setBusy(true); setErr(undefined)
    try { setResult(await api.commit(file)) } catch (e) {
      if (e instanceof ApiError && e.status === 422) setResult(e.body as ImportReport)
      else setErr(e instanceof Error ? e.message : String(e))
    } finally { setBusy(false) }
  }

  return (
    <div className="space-y-6">
      <PageHeader title="Importar banco de preguntas" subtitle="Sube un archivo JSON. Verás una vista previa con la validación antes de confirmar. El archivo original se conserva intacto." />
      <label htmlFor="file" className="card flex cursor-pointer flex-col items-center gap-3 border-2 border-dashed border-rose/40 py-10 text-center hover:bg-blush/10">
        <FileJson className="h-10 w-10 text-rose" aria-hidden />
        <span className="font-semibold">{file ? file.name : 'Selecciona un archivo .json'}</span>
        <span className="muted">Máximo 5 MB. La importación desde PDF llegará en una fase posterior.</span>
        <input id="file" type="file" accept=".json,application/json" className="sr-only" onChange={(e) => choose(e.target.files?.[0] ?? null)} />
      </label>
      {busy && <p className="muted" role="status">Procesando…</p>}
      {err && <p role="alert" className="rounded-lg bg-coral/10 p-3 text-sm text-coral">{err}</p>}
      {result ? <Report report={result} committed /> : preview && (
        <>
          <Report report={preview} />
          <div className="flex justify-end gap-2">
            <button className="btn-ghost" onClick={() => choose(null)}>Cancelar</button>
            <button className="btn-primary" disabled={busy || !preview.canCommit} onClick={commit}><Upload className="h-4 w-4" /> Confirmar importación</button>
          </div>
        </>
      )}
    </div>
  )
}

const statusText: Record<string, string> = {
  vista_previa: 'Vista previa', importado: 'Importado correctamente', sin_cambios: 'Sin cambios: este archivo ya estaba importado', rechazado: 'Rechazado',
}

function Report({ report, committed }: { report: ImportReport; committed?: boolean }) {
  const errors = report.issues.filter((i) => i.severity === 'error')
  const warnings = report.issues.filter((i) => i.severity !== 'error')
  const ok = report.canCommit && report.status !== 'rechazado'
  return (
    <div className="space-y-4">
      <div className={`card flex items-start gap-3 ${ok ? 'border-emerald-200 bg-emerald-50/50' : 'border-coral/30 bg-coral/5'}`}>
        {ok ? <CheckCircle2 className="h-6 w-6 text-emerald-600" aria-hidden /> : <AlertTriangle className="h-6 w-6 text-coral" aria-hidden />}
        <div className="flex-1">
          <p className="font-bold">{report.canCommit ? statusText[report.status] ?? report.status : statusText.rechazado}</p>
          <p className="muted">{report.title ?? 'Sin título'} · versión declarada {report.declaredVersion ?? '—'} · {report.fileName}</p>
          {!committed && report.alreadyImported && <p className="mt-1 text-sm text-[#9A5A0B]">Este archivo exacto ya fue importado; confirmar no generará cambios.</p>}
          {committed && report.courseId && <Link to={`/cursos/${report.courseId}`} className="mt-2 inline-block text-sm font-semibold text-rose">Ir al curso →</Link>}
        </div>
      </div>
      <div className="grid gap-3 sm:grid-cols-3 lg:grid-cols-6">
        {[
          ['Preguntas', report.totalFound], ['Declaradas', report.declaredTotal ?? '—'], ['Módulos', report.moduleCount],
          ['Únicas / múltiples', `${report.single} / ${report.multiple}`], ['En revisión', report.needsReview], ['Marcadas por validación', report.flaggedByValidation],
        ].map(([l, v]) => <div key={l as string} className="card p-4"><p className="text-xs text-night/60">{l}</p><p className="text-xl font-bold">{v}</p></div>)}
      </div>
      {(report.added + report.updated + report.unchanged + report.missingFromFile > 0) && (
        <p className="text-sm text-night/70">Nuevas: <b>{report.added}</b> · Actualizadas: <b>{report.updated}</b> · Sin cambios: <b>{report.unchanged}</b> · Ausentes en el archivo (se conservan): <b>{report.missingFromFile}</b></p>
      )}
      {report.issues.length > 0 && (
        <section className="card">
          <h2 className="mb-2 font-bold">Hallazgos de validación ({errors.length} errores, {warnings.length} advertencias)</h2>
          <p className="muted mb-3">Las claves dudosas no se corrigen automáticamente: las preguntas afectadas quedan en revisión y fuera de los exámenes por defecto.</p>
          <ul className="max-h-80 space-y-1 overflow-y-auto text-sm">
            {report.issues.map((i, n) => (
              <li key={n} className={`rounded-lg px-3 py-2 ${i.severity === 'error' ? 'bg-coral/10' : 'bg-amber/10'}`}>
                <b>{i.questionId ? `Pregunta ${i.questionId}` : 'Archivo'}:</b> {i.message} <span className="text-xs text-night/50">({i.code})</span>
              </li>
            ))}
          </ul>
        </section>
      )}
      {report.modules.length > 0 && (
        <details className="card">
          <summary className="cursor-pointer font-bold">Módulos detectados ({report.modules.length})</summary>
          <ul className="mt-3 grid gap-1 text-sm sm:grid-cols-2">
            {report.modules.map((m) => <li key={m.name} className="flex justify-between rounded px-2 py-1 odd:bg-night/[0.03]"><span>{m.name}</span><span>{m.total}{m.inReview ? ` · ${m.inReview} en revisión` : ''}</span></li>)}
          </ul>
        </details>
      )}
    </div>
  )
}
