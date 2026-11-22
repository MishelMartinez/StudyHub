import { UserPlus } from 'lucide-react'
import { useEffect, useState } from 'react'
import { api, getProfileId, setProfileId } from '../api'
import { ErrorState, Loading, PageHeader, useAsync } from '../components/ui'

export function ProfilePage() {
  const { data, error, loading, reload } = useAsync(api.profiles, [])
  const [activeId, setActiveId] = useState<number | null>(null)
  const [name, setName] = useState('')
  const [threshold, setThreshold] = useState('70')
  const [newName, setNewName] = useState('')
  const [msg, setMsg] = useState<{ ok: boolean; text: string }>()

  const active = data?.find((p) => p.id === activeId)
  useEffect(() => {
    if (!data?.length) return
    const id = data.find((p) => String(p.id) === getProfileId())?.id ?? data[0].id
    setActiveId(id)
  }, [data])
  useEffect(() => { if (active) { setName(active.name); setThreshold(String(active.passThreshold)) } }, [active])

  if (loading) return <Loading />
  if (error || !data) return <ErrorState message={error ?? 'Sin datos'} onRetry={reload} />

  const wrap = async (fn: () => Promise<unknown>, ok: string) => {
    setMsg(undefined)
    try { await fn(); setMsg({ ok: true, text: ok }); await reload() } catch (e) { setMsg({ ok: false, text: e instanceof Error ? e.message : String(e) }) }
  }
  const switchTo = (id: number) => { setProfileId(id); setActiveId(id); setMsg({ ok: true, text: 'Perfil activo cambiado.' }) }

  return (
    <div className="space-y-6">
      <PageHeader title="Perfil" subtitle="Tu progreso, marcas y exámenes se guardan por perfil en la base de datos local." />
      {msg && <p role="status" className={`rounded-lg p-3 text-sm ${msg.ok ? 'bg-emerald-50 text-emerald-700' : 'bg-coral/10 text-coral'}`}>{msg.text}</p>}
      <div className="grid gap-6 lg:grid-cols-2">
        <section className="card space-y-4">
          <h2 className="font-bold">Perfil activo</h2>
          <div>
            <label className="label" htmlFor="active">Seleccionar perfil</label>
            <select id="active" className="input" value={activeId ?? ''} onChange={(e) => switchTo(Number(e.target.value))}>
              {data.map((p) => <option key={p.id} value={p.id}>{p.name}</option>)}
            </select>
          </div>
          {active && (
            <form className="space-y-3" onSubmit={(e) => { e.preventDefault(); void wrap(() => api.updateProfile(active.id, { name, passThreshold: Number(threshold) }), 'Perfil actualizado.') }}>
              <div><label className="label" htmlFor="name">Nombre</label><input id="name" className="input" value={name} onChange={(e) => setName(e.target.value)} /></div>
              <div><label className="label" htmlFor="th">Umbral de aprobación (%)</label><input id="th" className="input" inputMode="numeric" value={threshold} onChange={(e) => setThreshold(e.target.value)} /></div>
              <button className="btn-primary">Guardar</button>
            </form>
          )}
        </section>
        <section className="card space-y-3">
          <h2 className="font-bold">Crear perfil</h2>
          <form className="space-y-3" onSubmit={(e) => {
            e.preventDefault()
            void wrap(async () => { const p = await api.createProfile(newName); setProfileId(p.id); setNewName('') }, 'Perfil creado y activado.')
          }}>
            <div><label className="label" htmlFor="new">Nombre</label><input id="new" className="input" value={newName} onChange={(e) => setNewName(e.target.value)} /></div>
            <button className="btn-dark" disabled={!newName.trim()}><UserPlus className="h-4 w-4" /> Crear</button>
          </form>
        </section>
      </div>
    </div>
  )
}
