import type { SourceChunk } from '@/types'

const BASE_URL = '/api'

export interface HistoryMessage {
  role: 'user' | 'assistant'
  content: string
}

export type StreamEvent =
  | { type: 'text'; delta: string }
  | { type: 'sources'; sources: SourceChunk[] }
  | { type: 'error'; message: string }

export async function chatOnce(message: string, history?: HistoryMessage[]): Promise<{ reply: string; sources: SourceChunk[] }> {
  const res = await fetch(`${BASE_URL}/chat`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, history }),
  })
  if (!res.ok) throw new Error(`Chat failed: ${res.statusText}`)
  return res.json() as Promise<{ reply: string; sources: SourceChunk[] }>
}

export async function* chatStreamFetch(
  message: string,
  history?: HistoryMessage[],
  signal?: AbortSignal
): AsyncGenerator<StreamEvent> {
  const res = await fetch(`${BASE_URL}/chat/stream`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ message, history }),
    signal,
  })
  if (!res.ok || !res.body) throw new Error(`Stream failed: ${res.statusText}`)

  const reader = res.body.getReader()
  const decoder = new TextDecoder()
  let currentEvent = ''

  try {
    while (true) {
      const { done, value } = await reader.read()
      if (done) break
      const text = decoder.decode(value, { stream: true })
      const lines = text.split('\n')
      for (const line of lines) {
        if (line.startsWith('event: ')) {
          currentEvent = line.slice(7).trim()
        } else if (line.startsWith('data: ')) {
          const data = line.slice(6)
          if (data.trimEnd() === '[DONE]') return
          if (!data) continue
          if (currentEvent === 'sources') {
            yield { type: 'sources', sources: JSON.parse(data) as SourceChunk[] }
            currentEvent = ''
          } else if (currentEvent === 'error') {
            const parsed = JSON.parse(data) as { error: string }
            yield { type: 'error', message: parsed.error }
            currentEvent = ''
          } else {
            yield { type: 'text', delta: data }
          }
        }
      }
    }
  } finally {
    reader.releaseLock()
  }
}

export async function listDocuments(): Promise<string[]> {
  const res = await fetch(`${BASE_URL}/documents`)
  if (!res.ok) throw new Error('Failed to list documents')
  return res.json() as Promise<string[]>
}

export interface UploadResult {
  fileName: string
  status: string
  chunkCount: number
}

export async function uploadDocument(file: File): Promise<UploadResult> {
  const form = new FormData()
  form.append('file', file)
  const res = await fetch(`${BASE_URL}/documents/upload`, {
    method: 'POST',
    body: form,
  })
  if (!res.ok) {
    let detail = res.statusText
    try {
      const body = await res.json() as Record<string, unknown>
      detail = (body.error ?? body.Error ?? body.detail ?? body.title ?? res.statusText) as string
    } catch { /* non-JSON response */ }
    throw new Error(String(detail))
  }
  return res.json() as Promise<UploadResult>
}

export async function deleteDocument(documentName: string): Promise<void> {
  const res = await fetch(`${BASE_URL}/documents/${encodeURIComponent(documentName)}`, {
    method: 'DELETE',
  })
  if (!res.ok && res.status !== 404) throw new Error(`Delete failed: ${res.statusText}`)
}
