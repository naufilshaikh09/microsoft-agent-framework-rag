export interface SourceChunk {
  documentName: string
  chunkIndex: number
  pageNumber: number
  excerpt: string
  score: number
}

export interface Message {
  id: string
  role: 'user' | 'assistant'
  content: string
  timestamp: Date
  isStreaming?: boolean
  isError?: boolean
  errorMessage?: string
  sources?: SourceChunk[]
}

export interface Conversation {
  id: string
  title: string
  messages: Message[]
  createdAt: Date
}
