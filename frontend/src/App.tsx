import { Route, Routes } from 'react-router-dom'
import { Layout } from './components/Layout'
import { Empty } from './components/ui'
import { DashboardPage } from './pages/Dashboard'
import { CoursesPage, CoursePage } from './pages/Courses'
import { StudyPage } from './pages/Study'
import { ExamConfigPage } from './pages/ExamConfig'
import { ExamPage } from './pages/Exam'
import { ExamHistoryPage } from './pages/ExamHistory'
import { ErrorLogPage } from './pages/ErrorLog'
import { RulesPage } from './pages/Rules'
import { MarksPage } from './pages/Marks'
import { ImportPage } from './pages/Import'
import { BankPage } from './pages/Bank'
import { ProfilePage } from './pages/Profile'

export default function App() {
  return (
    <Routes>
      <Route element={<Layout />}>
        <Route index element={<DashboardPage />} />
        <Route path="cursos" element={<CoursesPage />} />
        <Route path="cursos/:courseId" element={<CoursePage />} />
        <Route path="estudiar/:moduleId" element={<StudyPage />} />
        <Route path="examenes" element={<ExamHistoryPage />} />
        <Route path="examenes/nuevo" element={<ExamConfigPage />} />
        <Route path="examenes/:examId" element={<ExamPage />} />
        <Route path="errores" element={<ErrorLogPage />} />
        <Route path="reglas" element={<RulesPage />} />
        <Route path="favoritas" element={<MarksPage kind="favorito" />} />
        <Route path="repasar" element={<MarksPage kind="repasar" />} />
        <Route path="importar" element={<ImportPage />} />
        <Route path="banco" element={<BankPage />} />
        <Route path="perfil" element={<ProfilePage />} />
        <Route path="*" element={<Empty title="Página no encontrada">La ruta solicitada no existe.</Empty>} />
      </Route>
    </Routes>
  )
}
