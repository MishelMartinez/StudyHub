import { BookOpen, BookMarked, ClipboardList, Database, Heart, History, LayoutDashboard, Lightbulb, Menu, Repeat, Settings, Upload, X, XCircle } from 'lucide-react'
import { useEffect, useState } from 'react'
import { NavLink, Outlet, useLocation } from 'react-router-dom'
import { api, getProfileId, type Profile } from '../api'

const nav = [
  { to: '/', label: 'Inicio', icon: LayoutDashboard, end: true },
  { to: '/cursos', label: 'Cursos y módulos', icon: BookOpen },
  { to: '/examenes/nuevo', label: 'Nuevo examen', icon: ClipboardList },
  { to: '/examenes', label: 'Historial', icon: History, end: true },
  { to: '/errores', label: 'Error Log', icon: XCircle },
  { to: '/reglas', label: 'Reglas', icon: Lightbulb },
  { to: '/favoritas', label: 'Favoritas', icon: Heart },
  { to: '/repasar', label: 'Para repasar', icon: Repeat },
  { to: '/importar', label: 'Importar banco', icon: Upload },
  { to: '/banco', label: 'Banco y versiones', icon: Database },
  { to: '/perfil', label: 'Perfil', icon: Settings },
]

export function Layout() {
  const [open, setOpen] = useState(false)
  const [profile, setProfile] = useState<Profile | null>(null)
  const location = useLocation()
  useEffect(() => setOpen(false), [location.pathname])
  useEffect(() => {
    api.profiles().then((ps) => setProfile(ps.find((p) => String(p.id) === getProfileId()) ?? ps[0] ?? null)).catch(() => setProfile(null))
  }, [location.pathname])

  return (
    <div className="min-h-screen lg:flex">
      <a href="#contenido" className="sr-only focus:not-sr-only focus:absolute focus:left-2 focus:top-2 focus:z-50 focus:rounded focus:bg-white focus:p-2">Saltar al contenido</a>
      <header className="sticky top-0 z-30 flex items-center justify-between bg-night px-4 py-3 text-white lg:hidden">
        <Brand />
        <button className="rounded-lg p-2 hover:bg-white/10" onClick={() => setOpen(!open)} aria-label={open ? 'Cerrar menú' : 'Abrir menú'} aria-expanded={open}>
          {open ? <X className="h-6 w-6" /> : <Menu className="h-6 w-6" />}
        </button>
      </header>
      <aside className={`${open ? 'block' : 'hidden'} fixed inset-x-0 top-[60px] z-20 max-h-[calc(100vh-60px)] overflow-y-auto bg-night text-white lg:sticky lg:top-0 lg:block lg:h-screen lg:max-h-none lg:w-64 lg:shrink-0`}>
        <div className="hidden px-6 py-6 lg:block"><Brand /></div>
        <nav aria-label="Navegación principal" className="space-y-1 px-3 pb-6 lg:pb-0">
          {nav.map(({ to, label, icon: Icon, end }) => (
            <NavLink key={to} to={to} end={end}
              className={({ isActive }) => `flex items-center gap-3 rounded-xl px-3 py-2.5 text-sm font-medium transition ${isActive ? 'bg-rose text-white' : 'text-white/75 hover:bg-white/10 hover:text-white'}`}>
              <Icon className="h-4 w-4" aria-hidden /> {label}
            </NavLink>
          ))}
        </nav>
        {profile && (
          <div className="mx-3 mt-6 rounded-xl bg-white/5 p-3 text-xs text-white/70 lg:absolute lg:bottom-4 lg:left-0 lg:right-0">
            Perfil activo: <span className="font-semibold text-blush">{profile.name}</span>
          </div>
        )}
      </aside>
      <main id="contenido" className="mx-auto w-full max-w-6xl flex-1 px-4 py-6 sm:px-6 lg:px-10 lg:py-10">
        <Outlet />
      </main>
    </div>
  )
}

function Brand() {
  return (
    <div className="flex items-center gap-2">
      <span className="grid h-9 w-9 place-items-center rounded-xl bg-rose"><BookMarked className="h-5 w-5 text-white" aria-hidden /></span>
      <div className="leading-tight">
        <p className="font-bold">StudyHub</p>
        <p className="text-xs text-blush">AWS re/Start</p>
      </div>
    </div>
  )
}
