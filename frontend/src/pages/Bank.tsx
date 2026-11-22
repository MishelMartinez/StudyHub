import { Download, Search, X } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api, type AdminQuestionDetail } from '../api'
import { ErrorState, Loading, PageHeader, ReviewWarning, fmtDate, useAsync } from '../components/ui'

export function BankPage() {
  const banks = useAsync(api.banks, [])
  const [search, setSearch] = useState('')
  const [term, setTerm] = useState('')
  const [state, setState] = useState('')
  const [type, setType] = useState('')
  const [page, setPage] = useState(1)
  const [detail, setDetail] = useState<AdminQuestionDetail | null>(null)
  useEffect(() => { const t = setTimeout(() => { setTerm(search); setPage(1) }, 250); return () => clearTimeout(t) }, [search])
  const qs = useAsync(() => api.adminQuestions({ search: term || undefined, state: state || undefined, type: type || undefined, page, pageSize: 20 }), [term, state, type, page])
  const pages = qs.data ? Math.max(1, Math.ceil(qs.data.total / qs.data.pageSize)) : 1

  return (
    <div className="space-y-6">
      <PageHeader title="Banco y versiones" subtitle="Consulta las preguntas importadas y el historial de versiones del banco." />
      <section className="card">
        <h2 className="mb-3 font-bold">Versiones importadas</h2>
        {banks.loading ? <Loading /> : banks.error ? <ErrorState message={banks.error} onRetry={banks.reload} /> : (
          <div className="overflow-x-auto">
            <table className="w-full text-left text-sm">
              <thead className="text-xs uppercase text-night/60"><tr><th className="py-2 pr-4">Curso</th><th className="py-2 pr-4">Versión</th><th className="py-2 pr-4">Archivo</th><th className="py-2 pr-4">Fecha</th><th className="py-2 pr-4">Resultado</th><th /></tr></thead>
              <tbody className="divide-y divide-night/5">
                {banks.data?.map((b) => (
                  <tr key={b.id}>
                    <td className="py-2 pr-4">{b.courseTitle}</td>
                    <td className="py-2 pr-4">v{b.versionNumber} <span className="text-night/50">(declarada {b.declaredVersion})</span></td>
                    <td className="py-2 pr-4">{b.sourceFileName}</td>
                    <td className="py-2 pr-4">{fmtDate(b.importedAt)}</td>
                    <td className="py-2 pr-4">{b.report ? `${b.report.added} nuevas · ${b.report.updated} actualizadas` : '—'}</td>
                    <td className="py-2"><a className="btn-ghost px-2 py-1" href={`/api/admin/banks/${b.id}/file`} aria-label="Descargar archivo original"><Download className="h-4 w-4" /></a></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </section>

      <section className="card space-y-4">
        <div className="flex flex-wrap gap-2">
          <div className="relative min-w-[220px] flex-1">
            <Search className="absolute left-3 top-2.5 h-4 w-4 text-night/40" aria-hidden />
            <label htmlFor="q" className="sr-only">Buscar preguntas</label>
            <input id="q" className="input pl-9" placeholder="Buscar por texto, módulo o número…" value={search} onChange={(e) => setSearch(e.target.value)} />
          </div>
          <label className="sr-only" htmlFor="state">Estado</label>
          <select id="state" className="input w-auto" value={state} onChange={(e) => { setState(e.target.value); setPage(1) }}>
            <option value="">Todos los estados</option><option value="respuesta_razonada">Respuesta razonada</option><option value="requiere_revision">Requiere revisión</option>
          </select>
          <label className="sr-only" htmlFor="type">Tipo</label>
          <select id="type" className="input w-auto" value={type} onChange={(e) => { setType(e.target.value); setPage(1) }}>
            <option value="">Todos los tipos</option><option value="unica">Única</option><option value="multiple">Múltiple</option>
          </select>
        </div>
        {qs.loading ? <Loading /> : qs.error || !qs.data ? <ErrorState message={qs.error ?? 'Sin datos'} onRetry={qs.reload} /> : (
          <>
            <p className="muted">{qs.data.total} preguntas</p>
            <ul className="divide-y divide-night/5">
              {qs.data.items.map((q) => (
                <li key={q.id}>
                  <button className="flex w-full items-start gap-3 py-3 text-left hover:text-rose" onClick={() => api.adminQuestion(q.id).then(setDetail)}>
                    <span className="w-10 shrink-0 text-sm font-bold text-night/50">#{q.originalId}</span>
                    <span className="flex-1">
                      <span className="block text-sm font-medium">{q.text}</span>
                      <span className="text-xs text-night/50">{q.module} · {q.type === 'unica' ? 'Única' : 'Múltiple'} · v{q.currentVersion}</span>
                    </span>
                    {q.needsReview && <span className="chip shrink-0 bg-amber/15 text-[#9A5A0B]">En revisión</span>}
                  </button>
                </li>
              ))}
            </ul>
            <div className="flex items-center justify-end gap-2 text-sm">
              <button className="btn-ghost" disabled={page <= 1} onClick={() => setPage(page - 1)}>Anterior</button>
              <span>Página {page} de {pages}</span>
              <button className="btn-ghost" disabled={page >= pages} onClick={() => setPage(page + 1)}>Siguiente</button>
            </div>
          </>
        )}
      </section>

      {detail && (
        <div className="fixed inset-0 z-40 flex justify-end bg-night/40" role="dialog" aria-modal="true" aria-labelledby="detail-t" onClick={() => setDetail(null)}>
          <div className="h-full w-full max-w-xl space-y-4 overflow-y-auto bg-white p-6 shadow-xl" onClick={(e) => e.stopPropagation()}>
            <div className="flex items-start justify-between gap-2">
              <h2 id="detail-t" className="text-lg font-bold">Pregunta #{detail.summary.originalId}</h2>
              <button className="rounded-lg p-1 hover:bg-night/5" onClick={() => setDetail(null)} aria-label="Cerrar"><X className="h-5 w-5" /></button>
            </div>
            <p className="muted">{detail.summary.module} · estado original: {detail.summary.originalState}</p>
            {detail.summary.needsReview && <ReviewWarning notes={detail.reviewNotes} />}
            <p className="font-semibold">{detail.summary.text}</p>
            <ul className="space-y-1 text-sm">
              {detail.options.map((o) => <li key={o.key} className={`rounded-lg px-3 py-2 ${detail.correctAnswers.includes(o.key) ? 'bg-emerald-50 text-emerald-800' : 'bg-night/[0.03]'}`}><b>{o.key}.</b> {o.text}</li>)}
            </ul>
            <p className="text-sm"><b>Clave del banco:</b> {detail.correctAnswers.join(', ')}</p>
            <p className="text-sm"><b>Explicación:</b> {detail.explanation}</p>
            <p className="text-sm"><b>Regla:</b> {detail.memoryRule}</p>
            {detail.source && <p className="text-xs text-night/50">Fuente: {detail.source}</p>}
            <div>
              <p className="mb-1 text-sm font-bold">Historial de versiones</p>
              <ul className="space-y-1 text-xs">
                {detail.versions.map((v) => <li key={v.versionNumber} className="rounded bg-night/[0.03] px-2 py-1">v{v.versionNumber} · {fmtDate(v.createdAt)} · <code>{v.contentHash.slice(0, 12)}</code></li>)}
              </ul>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}
