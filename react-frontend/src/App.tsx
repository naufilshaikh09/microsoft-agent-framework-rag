import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { Sidebar } from '@/components/layout/Sidebar'
import { Header } from '@/components/layout/Header'
import { ChatWindow } from '@/components/chat/ChatWindow'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,
      retry: 1,
    },
  },
})

export default function App() {
  return (
    <QueryClientProvider client={queryClient}>
      <div className="flex h-screen bg-[#0f0f0f] text-gray-100 overflow-hidden">
        <Sidebar />
        <div className="flex flex-col flex-1 min-w-0">
          <Header />
          <ChatWindow />
        </div>
      </div>
    </QueryClientProvider>
  )
}
