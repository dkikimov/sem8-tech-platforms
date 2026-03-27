import { createContext, useContext } from 'react'
import type { PagedCallRecords, SubscriberSummary } from '../../../api'

export type TarifficationResultsState = {
  summary: SubscriberSummary[]
  callRecords: PagedCallRecords | null
  phoneFilter: string
  setSummary: (value: SubscriberSummary[]) => void
  setCallRecords: (value: PagedCallRecords | null) => void
  setPhoneFilter: (value: string) => void
  resetResults: () => void
}

export const TarifficationResultsContext = createContext<TarifficationResultsState | null>(null)

export function useTarifficationResultsState() {
  const ctx = useContext(TarifficationResultsContext)
  if (!ctx) {
    throw new Error('useTarifficationResultsState must be used within TarifficationResultsProvider')
  }
  return ctx
}
