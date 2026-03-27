import { useMemo, useState } from 'react'
import type { ReactNode } from 'react'
import type { PagedCallRecords, SubscriberSummary } from '../../../api'
import { TarifficationResultsContext } from './TarifficationResultsContext'
import type { TarifficationResultsState } from './TarifficationResultsContext'

export function TarifficationResultsProvider(props: { children: ReactNode }) {
  const [summary, setSummary] = useState<SubscriberSummary[]>([])
  const [callRecords, setCallRecords] = useState<PagedCallRecords | null>(null)
  const [phoneFilter, setPhoneFilter] = useState('')

  const value = useMemo<TarifficationResultsState>(
    () => ({
      summary,
      callRecords,
      phoneFilter,
      setSummary,
      setCallRecords,
      setPhoneFilter,
      resetResults: () => {
        setSummary([])
        setCallRecords(null)
        setPhoneFilter('')
      },
    }),
    [callRecords, phoneFilter, summary],
  )

  return <TarifficationResultsContext.Provider value={value}>{props.children}</TarifficationResultsContext.Provider>
}
