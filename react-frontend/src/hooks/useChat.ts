import { useState, useCallback, useRef, useEffect } from 'react'
import { chatStreamFetch } from '@/api/client'
import type { HistoryMessage } from '@/api/client'
import type { Message } from '@/types'

const STORAGE_KEY = 'agentframeworkrag:chat'
const LEGACY_STORAGE_KEY = 'semanticrag:chat'

function loadMessages(): Message[] {
  try {
    let raw = localStorage.getItem(STORAGE_KEY)
    if (!raw) {
      raw = localStorage.getItem(LEGACY_STORAGE_KEY)
      if (raw) {
        localStorage.setItem(STORAGE_KEY, raw)
        localStorage.removeItem(LEGACY_STORAGE_KEY)
      }
    }
    if (!raw) return []
    const parsed = JSON.parse(raw) as Message[]
    // Revive Date fields that were serialised as strings
    return parsed.map(m => ({ ...m, timestamp: new Date(m.timestamp), isStreaming: false }))
  } catch {
    return []
  }
}

function saveMessages(messages: Message[]) {
  try {
    // Only persist completed messages (not mid-stream state)
    const toSave = messages.filter(m => !m.isStreaming)
    localStorage.setItem(STORAGE_KEY, JSON.stringify(toSave))
  } catch { /* quota exceeded or private browsing */ }
}

export function useChat() {
  const [messages, setMessages] = useState<Message[]>(loadMessages)
  const [isStreaming, setIsStreaming] = useState(false)
  const abortRef = useRef<AbortController | null>(null)

  // Persist messages to localStorage whenever they change (completed messages only)
  useEffect(() => {
    saveMessages(messages)
  }, [messages])

  const sendMessage = useCallback(async (text: string) => {
    if (isStreaming) return

    const userMsg: Message = {
      id: crypto.randomUUID(),
      role: 'user',
      content: text,
      timestamp: new Date(),
    }

    const assistantId = crypto.randomUUID()
    const assistantMsg: Message = {
      id: assistantId,
      role: 'assistant',
      content: '',
      timestamp: new Date(),
      isStreaming: true,
    }

    // Capture history before state update — does not include the current message
    const historySlice: HistoryMessage[] = messages
      .filter(m => !m.isStreaming)
      .slice(-10)
      .map(m => ({ role: m.role, content: m.content }))

    setMessages(prev => [...prev, userMsg, assistantMsg])
    setIsStreaming(true)

    abortRef.current = new AbortController()

    try {
      for await (const event of chatStreamFetch(text, historySlice, abortRef.current.signal)) {
        if (event.type === 'sources') {
          setMessages(prev =>
            prev.map(m =>
              m.id === assistantId ? { ...m, sources: event.sources } : m
            )
          )
        } else if (event.type === 'text') {
          setMessages(prev =>
            prev.map(m =>
              m.id === assistantId ? { ...m, content: m.content + event.delta } : m
            )
          )
        } else if (event.type === 'error') {
          setMessages(prev =>
            prev.map(m =>
              m.id === assistantId
                ? { ...m, content: m.content || '', isError: true, errorMessage: event.message }
                : m
            )
          )
        }
      }
    } catch (err) {
      if ((err as Error).name !== 'AbortError') {
        setMessages(prev =>
          prev.map(m =>
            m.id === assistantId
              ? { ...m, content: m.content || '', isError: true, errorMessage: 'Failed to get a response. Please try again.' }
              : m
          )
        )
      }
    } finally {
      setMessages(prev =>
        prev.map(m =>
          m.id === assistantId ? { ...m, isStreaming: false } : m
        )
      )
      setIsStreaming(false)
      abortRef.current = null
    }
  }, [isStreaming, messages])

  const clearMessages = useCallback(() => {
    setMessages([])
    localStorage.removeItem(STORAGE_KEY)
  }, [])

  const stopStreaming = useCallback(() => {
    abortRef.current?.abort()
  }, [])

  return { messages, isStreaming, sendMessage, clearMessages, stopStreaming }
}
