import { useEffect, useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import { TarifficationSessionContext } from './TarifficationSessionContext'
import type { TarifficationSessionState } from './TarifficationSessionContext'
import type { ProgressEventPayload } from '../../../api'

const storageKey = 'tp.tariffication.sessionId'

export function TarifficationSessionProvider(props: { children: ReactNode }) {
  const [sessionId, setSessionId] = useState<string | null>(() => sessionStorage.getItem(storageKey))
  const [startedAtMs, setStartedAtMs] = useState<number | null>(null)
  const [finishedAtMs, setFinishedAtMs] = useState<number | null>(null)
  const [processingStatus, setProcessingStatus] = useState('')
  const [progress, setProgress] = useState<ProgressEventPayload | null>(null)

  useEffect(() => {
    if (sessionId) sessionStorage.setItem(storageKey, sessionId)
    else sessionStorage.removeItem(storageKey)
  }, [sessionId])

  const value = useMemo<TarifficationSessionState>(
    () => ({
      sessionId,
      startedAtMs,
      finishedAtMs,
      processingStatus,
      progress,

      setSessionId,
      setStartedAtMs,
      setFinishedAtMs,
      setProcessingStatus,
      setProgress,

      resetSession: () => {
        setSessionId(null)
        setStartedAtMs(null)
        setFinishedAtMs(null)
        setProcessingStatus('')
        setProgress(null)
      },
    }),
    [finishedAtMs, processingStatus, progress, sessionId, startedAtMs],
  )

  return <TarifficationSessionContext.Provider value={value}>{props.children}</TarifficationSessionContext.Provider>
}
