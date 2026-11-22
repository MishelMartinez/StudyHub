import { CheckCircle2, Lightbulb, XCircle } from 'lucide-react'
import type { Feedback, OptionPublic } from '../api'
import { ReviewWarning } from './ui'

interface Props {
  text: string
  type: 'unica' | 'multiple'
  options: OptionPublic[]
  selected: string[]
  onChange: (selected: string[]) => void
  feedback?: Feedback | null
  disabled?: boolean
  needsReview?: boolean
  reviewNotes?: string[]
  header?: React.ReactNode
}

export function QuestionView({ text, type, options, selected, onChange, feedback, disabled, needsReview, reviewNotes = [], header }: Props) {
  const locked = disabled || !!feedback
  const toggle = (key: string) => {
    if (locked) return
    if (type === 'unica') onChange([key])
    else onChange(selected.includes(key) ? selected.filter((k) => k !== key) : [...selected, key])
  }
  const fbByKey = new Map(feedback?.options.map((o) => [o.key, o]) ?? [])

  return (
    <div className="space-y-4">
      {header}
      {needsReview && <ReviewWarning notes={reviewNotes} />}
      <p className="whitespace-pre-line text-lg font-semibold leading-relaxed">{text}</p>
      <p className="muted">{type === 'multiple' ? 'Selección múltiple: marca todas las opciones correctas.' : 'Selección única: elige una opción.'}</p>
      <fieldset className="space-y-2">
        <legend className="sr-only">Opciones</legend>
        {options.map((o) => {
          const fb = fbByKey.get(o.key)
          const isSel = selected.includes(o.key)
          let cls = 'border-night/15 bg-white hover:border-rose/60'
          if (isSel && !feedback) cls = 'border-rose bg-blush/20'
          if (fb?.isCorrect) cls = 'border-emerald-500 bg-emerald-50'
          else if (fb?.selected) cls = 'border-coral bg-coral/10'
          return (
            <label key={o.key} className={`flex cursor-pointer items-start gap-3 rounded-xl border-2 p-3 transition ${cls} ${locked ? 'cursor-default' : ''}`}>
              <input
                type={type === 'unica' ? 'radio' : 'checkbox'}
                name="option"
                className="mt-1 h-4 w-4 accent-[#D95F98]"
                checked={isSel}
                disabled={locked}
                onChange={() => toggle(o.key)}
              />
              <span className="font-bold text-night/70">{o.key}.</span>
              <span className="flex-1">{o.text}</span>
              {fb?.isCorrect && <CheckCircle2 className="h-5 w-5 shrink-0 text-emerald-600" aria-label="Correcta" />}
              {fb && !fb.isCorrect && fb.selected && <XCircle className="h-5 w-5 shrink-0 text-coral" aria-label="Incorrecta" />}
            </label>
          )
        })}
      </fieldset>
      {feedback && <FeedbackPanel feedback={feedback} />}
    </div>
  )
}

export function FeedbackPanel({ feedback }: { feedback: Feedback }) {
  return (
    <div className="space-y-3" aria-live="polite">
      <div className={`flex items-center gap-2 rounded-xl p-3 font-semibold ${feedback.isCorrect ? 'bg-emerald-50 text-emerald-700' : 'bg-coral/10 text-coral'}`}>
        {feedback.isCorrect ? <CheckCircle2 className="h-5 w-5" aria-hidden /> : <XCircle className="h-5 w-5" aria-hidden />}
        {feedback.isCorrect
          ? '¡Correcto!'
          : feedback.selected.length === 0
            ? `Sin responder. Respuesta correcta: ${feedback.correctAnswers.join(', ')}`
            : `Incorrecto. Respuesta correcta: ${feedback.correctAnswers.join(', ')}`}
      </div>
      {feedback.explanation && (
        <div className="rounded-xl bg-night/[0.03] p-4">
          <p className="mb-1 text-sm font-semibold text-night/70">Explicación</p>
          <p className="whitespace-pre-line leading-relaxed">{feedback.explanation}</p>
        </div>
      )}
      {feedback.memoryRule && (
        <div className="flex gap-3 rounded-xl border border-amber/40 bg-amber/10 p-4">
          <Lightbulb className="mt-0.5 h-5 w-5 shrink-0 text-amber" aria-hidden />
          <div>
            <p className="text-sm font-semibold text-night/70">Regla para memorizar</p>
            <p className="leading-relaxed">{feedback.memoryRule}</p>
          </div>
        </div>
      )}
      {feedback.source && <p className="text-xs text-night/50">Fuente: {feedback.source}</p>}
    </div>
  )
}
